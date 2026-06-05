using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports.Outbound;
using System.Globalization;

namespace StockPriceSheetPrintService.Service.Application
{
	public class PortfolioReporter(
		ILogger<PortfolioReporter> logger,
		IDiscordNotifier discordNotifier,
		IGoogleSheetsClient googleSheetsClient,
		IConfiguration configuration,
		IPendingReportStore pendingReportStore) : IPortfolioReporter
	{
		private readonly ILogger<PortfolioReporter> _logger = logger;
		private readonly IDiscordNotifier _discordNotifier = discordNotifier;
		private readonly IGoogleSheetsClient _googleSheetsClient = googleSheetsClient;
		private readonly IConfiguration _configuration = configuration;
		private readonly IPendingReportStore _pendingReportStore = pendingReportStore;

		private const string TimeZoneId = "Central European Standard Time";
		private const int ReportHourLocal = 7; // 07:00 lokal tid (DST håndteres automatisk af TimeZoneInfo)

		public async Task ReportMorningAsync(decimal saxoBalance, decimal nordnetValue, decimal juneValue, decimal total, decimal previousDayValue, List<Transfer> newTransfers, bool sendDiscordImmediately, string? geminiInsights, CancellationToken ct)
		{
			try
			{
				var transferAmount = newTransfers.Count > 0 ? newTransfers.Sum(t => t.Amount) : (decimal?)null;

				if (sendDiscordImmediately)
				{
					_logger.LogInformation("[REPORTER] Manual trigger - sending Discord notification now");
					await _discordNotifier.SendMorningReportAsync(saxoBalance, nordnetValue, juneValue, total, previousDayValue, transferAmount, geminiInsights, ct);
					_logger.LogInformation("[REPORTER] Morning report sent at {time} UTC", DateTime.UtcNow);
					return;
				}

				// Konverter server tid (UTC) til lokal tid
				var localZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
				var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, localZone);

				// Sæt target til 07:00 lokal tid (TimeZoneInfo håndterer DST automatisk)
				var todayTargetLocal = nowLocal.Date + new TimeSpan(ReportHourLocal, 0, 0);

				// Hvis vi allerede har passeret 07:00 i dag, så plan for i morgen
				if (nowLocal > todayTargetLocal)
					todayTargetLocal = todayTargetLocal.AddDays(1);

				// Beregn forsinkelse fra server tid til target tid
				var delayUntilReport = todayTargetLocal - nowLocal;
				_logger.LogInformation("[REPORTER] Next report scheduled at {localTime} (in {hours}h {minutes}m)",
					todayTargetLocal, (int)delayUntilReport.TotalHours, delayUntilReport.Minutes);

				_pendingReportStore.Set(new ScheduledReport(saxoBalance, nordnetValue, juneValue, total, previousDayValue, transferAmount, geminiInsights, DateTime.UtcNow));


				_ = Task.Run(async () =>
				{
					await Task.Delay(delayUntilReport, CancellationToken.None);
					var report = _pendingReportStore.Get();
					if (report is null) return;
					try
					{
						await _discordNotifier.SendMorningReportAsync(saxoBalance, nordnetValue, juneValue, total, previousDayValue, transferAmount, geminiInsights, CancellationToken.None);
						_logger.LogInformation("[REPORTER] Morning report sent at {time} UTC", DateTime.UtcNow);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "[REPORTER] Failed to send scheduled report");
					}
				}, CancellationToken.None);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[REPORTER] Error in morning report handling");
			}
		}

		public async Task UpdateGoogleSheetsAsync(decimal total, CancellationToken ct)
		{
			try
			{
				var spreadsheetId = _configuration["SheetsApi:SheetsKey"];
				const string sheetName = "Daily";

				if (string.IsNullOrEmpty(spreadsheetId))
				{
					_logger.LogWarning("[REPORTER] SheetsApi:SheetsKey configuration missing, skipping update");
					return;
				}

				await _googleSheetsClient.UpdateGoogleSheetsCellAsync(spreadsheetId, sheetName, total.ToString("N2", CultureInfo.GetCultureInfo("da-DK")), ct);
				_logger.LogInformation("[REPORTER] Google Sheets updated with value: {value:F2} DKK", total);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[REPORTER] Failed to update Google Sheets");
			}
		}
	}
}
