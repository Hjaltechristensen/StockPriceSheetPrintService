using StockPriceSheetPrintService.Service.Ports.Inbound;
using StockPriceSheetPrintService.Service.Ports.Outbound;
using StockPriceSheetPrintService.Service.Ports.Persistence;

namespace StockPriceSheetPrintService.Service.Application
{
	public class PortfolioJobRunner(
		ILogger<PortfolioJobRunner> logger,
		IExecutionGuard executionGuard,
		IPortfolioDataFetcher dataFetcher,
		IPortfolioReporter reporter) : IPortfolioJobRunner
	{
		private readonly ILogger<PortfolioJobRunner> _logger = logger;
		private readonly IExecutionGuard _executionGuard = executionGuard;
		private readonly IPortfolioDataFetcher _dataFetcher = dataFetcher;
		private readonly IPortfolioReporter _reporter = reporter;

		public async Task RunJobAsync(CancellationToken ct, bool sendDiscordImmediately = false)
		{
			_logger.LogInformation("╔═══════════════════════════════════════════╗");
			_logger.LogInformation("║  JOB RUNNING START - {time:HH:mm:ss} UTC        ║", DateTime.UtcNow);
			_logger.LogInformation("╚═══════════════════════════════════════════╝");

			if (!_executionGuard.IsExecutionSafe())
			{
				_logger.LogWarning("[JOB] ✗ EXECUTION BLOCKED: Safety mechanism activated (too many runs)");
				return;
			}

			try
			{
				// Fetch all data in parallel
				_logger.LogInformation("[JOB] [1/3] Fetching portfolio data...");
				var saxoBalanceTask = _dataFetcher.GetSaxoBalanceAsync(ct);
				var nordnetValueTask = _dataFetcher.GetNordnetValueAsync(ct);
				var juneValueTask = _dataFetcher.GetJuneValueAsync(ct);
				var transfersTask = _dataFetcher.GetNewTransfersAsync(ct);
				var previousDayValueTask = _dataFetcher.GetPreviousDayValueAsync(ct);

				await Task.WhenAll(saxoBalanceTask, nordnetValueTask, juneValueTask, transfersTask, previousDayValueTask);

				var saxoBalance = saxoBalanceTask.Result;
				var nordnetValue = nordnetValueTask.Result;
				var juneValue = juneValueTask.Result;
				var newTransfers = transfersTask.Result;
				var previousDayValue = previousDayValueTask.Result;

				var total = saxoBalance + nordnetValue + juneValue;

				_logger.LogInformation("[JOB] ✓ Portfolio values fetched");
				_logger.LogInformation("[JOB]   Saxo: {saxo:F2} | Nordnet: {nordnet:F2} | June: {june:F2} | Total: {total:F2}", 
					saxoBalance, nordnetValue, juneValue, total);

				// Update Google Sheets
				_logger.LogInformation("[JOB] [2/3] Updating Google Sheets...");
				await _reporter.UpdateGoogleSheetsAsync(total, ct);
				_executionGuard.LogExecution();

				// Report results
				_logger.LogInformation("[JOB] [3/3] Reporting results...");
				await _reporter.ReportMorningAsync(saxoBalance, nordnetValue, juneValue, total, previousDayValue, newTransfers, sendDiscordImmediately, ct);

				LogJobCompleted(total);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[JOB] ✗ UNEXPECTED ERROR during job execution!");
				_logger.LogError("[JOB] Exception Type: {type}", ex.GetType().Name);
				_logger.LogError("[JOB] Stack Trace: {stackTrace}", ex.StackTrace);
				throw;
			}
		}

		private void LogJobCompleted(decimal total)
		{
			string totalLine = $"║  Total værdi: {total:F2} DKK";
			totalLine = totalLine.PadRight(44) + "║";

			_logger.LogInformation("╔═══════════════════════════════════════════╗");
			_logger.LogInformation("║  JOB COMPLETED - SUCCESSFULLY             ║");
			_logger.LogInformation(totalLine);
			_logger.LogInformation("╚═══════════════════════════════════════════╝");
		}
	}
}
