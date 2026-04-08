using StockPriceSheetPrintService.Outbound.Filesystem;
using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockPriceSheetPrintService.Service.Application
{
	public class PortfolioJobRunner(
		IHttpClientFactory httpClientFactory,
		ILogger<PortfolioJobRunner> logger,
		IConfiguration configuration,
		IExecutionGuard executionGuard,
		PortfolioCalculator portfolioCalculator,
		IGoogleSheetsClient googleSheetsClient,
		IDiscordNotifier discordNotifier,
		ISaxoTokenService saxoTokenService,
		ISaxoService saxoService,
		ISeenTransferStore seenTransferStore) : IPortfolioJobRunner
	{
		private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
		private readonly ILogger<PortfolioJobRunner> _logger = logger;
		private readonly IConfiguration _configuration = configuration;
		private readonly IExecutionGuard _executionGuard = executionGuard;
		private readonly PortfolioCalculator _portfolioCalculator = portfolioCalculator;
		private readonly IGoogleSheetsClient _googleSheetsClient = googleSheetsClient;
		private readonly IDiscordNotifier _discordNotifier = discordNotifier;
		private readonly ISaxoTokenService _saxoTokenService = saxoTokenService;
		private readonly ISaxoService _saxoService = saxoService;
		private readonly ISeenTransferStore _seenTransferStore = seenTransferStore;

		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
			Converters = { new FlexibleDateTimeOffsetConverter() }
		};

		private static bool IsInTransferWindow()
		{
			var today = DateTime.UtcNow;
			return today.Day >= 28 || today.Day <= 10;
		}


		public async Task RunJobAsync(CancellationToken ct, bool sendDiscordImmediately = false)
		{
			_logger.LogInformation("╔═══════════════════════════════════════════╗");
			_logger.LogInformation("║  JOB KØRSEL STARTER - {time:HH:mm:ss} UTC        ║", DateTime.UtcNow);
			_logger.LogInformation("╚═══════════════════════════════════════════╝");

			if (!_executionGuard.IsExecutionSafe())
			{
				_logger.LogWarning("[JOB] ✗ KØRSEL BLOKERET: Sikkerhedsmekanisme aktiveret (for mange kørsler)");
				return;
			}

			try
			{
				decimal nordnetCash = 1176m;
				decimal saxoBalance = 0m;
				decimal stockValue = 0m;
				decimal juneValue = 0m;
				string runningTotal = "=";
				List<SaxoTransaction> newTransfers = [];

				// --- 1. SAXO BALANCE ---
				_logger.LogInformation("[JOB] [1/4] Starter Saxo balance hentning...");
				string? saxoToken = await _saxoTokenService.GetAccessTokenAsync(ct);
				if (saxoToken != null)
				{
					saxoBalance = await GetSaxoBalanceAsync(saxoToken, ct);
					runningTotal += "+" + saxoBalance.ToString(new CultureInfo("da-DK"));
					_logger.LogInformation("[JOB] ✓ Saxo balance: {val:F2} DKK", saxoBalance);
					
					if (IsInTransferWindow())
					{
						newTransfers = await CheckForNewTransfersAsync(saxoToken, ct);
					}
				}
				else
				{
					_logger.LogWarning("[JOB] ⚠ Saxo balance ikke tilgængelig (log ind første gang via /saxo/login)");
				}

				// --- 2. AKTIER ---
				_logger.LogInformation("[JOB] [2/4] Starter hentning af Nordnet priser...");
				var eodResponse = await GetStockPricesAsync(ct);
				if (eodResponse != null)
				{
					stockValue = await _portfolioCalculator.CalculateTotalStockValueAsync(eodResponse, ct);
					stockValue += nordnetCash;
					runningTotal += "+" + stockValue.ToString(new CultureInfo("da-DK"));
					_logger.LogInformation("[JOB] ✓ Aktieværdi: {val:F2} DKK", stockValue);
				}
				else
				{
					_logger.LogWarning("[JOB] ⚠ Kunne ikke hente aktiepriser");
				}

				// --- 3. FONDE ---
				_logger.LogInformation("[JOB] [3/4] Starter hentning af fondsværdi...");
				juneValue = await _portfolioCalculator.FindTotalJuneValueAsync(ct);
				runningTotal += "+" + juneValue.ToString(new CultureInfo("da-DK"));
				_logger.LogInformation("[JOB] ✓ Fondsværdi: {val:F2} DKK", juneValue);

				// --- 4. GOOGLE SHEETS ---
				_logger.LogInformation("[JOB] [4/4] TOTAL værdi: {total} DKK - Sendes til Google Sheets...", runningTotal);
				var sheetsKey = _configuration["SheetsApi:SheetsKey"];
				var dayBeforeValue = 0m;
				if (!string.IsNullOrEmpty(sheetsKey))
				{
					dayBeforeValue = await _googleSheetsClient.UpdateGoogleSheetsCellAsync(sheetsKey, "Daily", runningTotal, ct);
					_executionGuard.LogExecution();
					_logger.LogInformation("[JOB] ✓ Google Sheets opdateret succesfuldt");
				}
				else
				{
					_logger.LogError("[JOB] ✗ FEJL: SheetsKey mangler! Kunne ikke gemme resultatet.");
				}

				var total = ParseRunningTotal(runningTotal);

				await HandleDiscordNotificationAsync(saxoBalance, stockValue, juneValue, total, dayBeforeValue, sendDiscordImmediately, newTransfers, ct);

				LogJobCompleted(total);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[JOB] ✗ UVENTET FEJL under job kørsel!");
				_logger.LogError("[JOB] Exception Type: {type}", ex.GetType().Name);
				_logger.LogError("[JOB] Stack Trace: {stackTrace}", ex.StackTrace);
				throw;
			}
		}

		private async Task<List<SaxoTransaction>> CheckForNewTransfersAsync(string accessToken, CancellationToken ct)
		{
			var fromDate = DateTime.UtcNow.AddDays(-14);
			var toDate = DateTime.UtcNow;

			var response = await _saxoService.GetSaxoTransactionsAsync(accessToken, fromDate, toDate, ct);
			var seenIds = await _seenTransferStore.LoadAsync(ct);

			var newTransfers = response.Data
				.Where(t => !seenIds.Contains(t.BookingId))
				.ToList();

			if (newTransfers.Count > 0)
				await _seenTransferStore.SaveAsync(seenIds, newTransfers.Select(t => t.BookingId), ct);

			return newTransfers;
		}
		private async Task<decimal> GetSaxoBalanceAsync(string accessToken, CancellationToken ct)
		{
			try
			{
				var response = await _saxoService.GetBalanceAsync(accessToken, ct);
				
				if (response is not null)
				{
					return response.TotalValue;
				}
				return 0m;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[SAXO-BALANCE] ✗ UVENTET FEJL ved hentning af balance!");
				return 0;
			}
		}

		private async Task<EodResponse?> GetStockPricesAsync(CancellationToken ct)
		{
			var client = _httpClientFactory.CreateClient("StockApi");
			var symbolsQuery = string.Join(",", AllTickers.Symbols.Keys);

			var response = await client.GetAsync(
				$"v2/eod/latest?access_key={_configuration["StockApi:AccessKey"]}&symbols={symbolsQuery}", ct);

			if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
			{
				response = await client.GetAsync(
					$"v2/eod/latest?access_key={_configuration["StockApi:AccessKey2"]}&symbols={symbolsQuery}", ct);
			}

			response.EnsureSuccessStatusCode();

			var json = await response.Content.ReadAsStringAsync(ct);
			_logger.LogInformation("[API-DEBUG] Marketstack response: {json}", json);

			using var doc = JsonDocument.Parse(json);
			if (!doc.RootElement.TryGetProperty("data", out var dataEl))
			{
				_logger.LogError("[API-DEBUG] Uventet JSON-struktur fra Marketstack: {json}",
					json.Substring(0, Math.Min(500, json.Length)));
				return null;
			}

			var data = JsonSerializer.Deserialize<List<EodDatum>>(dataEl.GetRawText(), JsonOptions);
			return new EodResponse { Data = data ?? [] };
		}

		private async Task HandleDiscordNotificationAsync(
			decimal saxoBalance, decimal stockValue, decimal juneValue,
			decimal total, decimal dayBeforeValue,
			bool sendDiscordImmediately, List<SaxoTransaction> newTransfers, CancellationToken ct)
		{
			var transferAmount = newTransfers?.Any() == true ? newTransfers.Sum(t => t.BookedAmount) : (decimal?)null;

			if (sendDiscordImmediately)
			{
				_logger.LogInformation("[DISCORD] Manuel trigger - sender Discord notifikation nu");
				try
				{
					await _discordNotifier.SendMorningReportAsync(saxoBalance, stockValue, juneValue, total, dayBeforeValue, transferAmount, ct);
					_logger.LogInformation("[DISCORD] Morning report sendt kl. {time} UTC", DateTime.UtcNow);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "[DISCORD] Fejl ved afsendelse af morning report");
				}
				return;
			}

			var localZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
			var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, localZone);
			var todayTargetLocal = nowLocal.Date + new TimeSpan(7, 0, 0);

			if (nowLocal > todayTargetLocal)
				todayTargetLocal = todayTargetLocal.AddDays(1);

			var delay = TimeZoneInfo.ConvertTimeToUtc(todayTargetLocal, localZone) - DateTime.UtcNow;
			_logger.LogInformation("[DISCORD] Discord notifikation planlagt til kl. 07:00 (om {hours:F1} timer)", delay.TotalHours);

			_ = Task.Run(async () =>
			{
				try
				{
					await Task.Delay(delay, CancellationToken.None);
					await _discordNotifier.SendMorningReportAsync(saxoBalance, stockValue, juneValue, total, dayBeforeValue, transferAmount, CancellationToken.None);
					_logger.LogInformation("[DISCORD] Morning report sendt kl. {time} UTC", DateTime.UtcNow);
				}
				catch (OperationCanceledException)
				{
					_logger.LogInformation("[DISCORD] Task annulleret før publicering");
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "[DISCORD] Fejl ved afsendelse af morning report");
				}
			}, CancellationToken.None);
		}

		private decimal ParseRunningTotal(string runningTotal)
		{
			var daDK = CultureInfo.GetCultureInfo("da-DK");
			return runningTotal
				.TrimStart('=')
				.Split('+')
				.Where(part => !string.IsNullOrWhiteSpace(part))
				.Sum(part =>
				{
					var trimmed = System.Text.RegularExpressions.Regex.Replace(part, @"(\,\d{10})\d+", "$1");
					return decimal.Parse(trimmed, daDK);
				});
		}

		private void LogJobCompleted(decimal total)
		{
			string totalLine = $"║  Total værdi: {total:F2} DKK";
			totalLine = totalLine.PadRight(44) + "║";

			_logger.LogInformation("╔═══════════════════════════════════════════╗");
			_logger.LogInformation("║  JOB AFSLUTTET - SUCCESFULDT              ║");
			_logger.LogInformation(totalLine);
			_logger.LogInformation("╚═══════════════════════════════════════════╝");
		}

		public class FlexibleDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
		{
			private static readonly string[] Formats =
			{
			"yyyy-MM-dd'T'HH:mm:sszzz",
			"yyyy-MM-dd'T'HH:mm:sszz"
		};

			public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			{
				var raw = reader.GetString()!;
				return DateTimeOffset.ParseExact(raw, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None);
			}

			public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
				=> writer.WriteStringValue(value.ToString("yyyy-MM-dd'T'HH:mm:sszzz"));
		}
	}
}
