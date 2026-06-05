using StockPriceSheetPrintService.Service.Ports.Inbound;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Service.Application
{
	public class StockpriceWorker(
		ILogger<StockpriceWorker> logger,
		ISaxoTokenService saxoTokenService,
		IPortfolioJobRunner jobRunner,
		SchedulerStatusStore statusStore) : BackgroundService
	{
		private readonly ILogger<StockpriceWorker> _logger = logger;
		private readonly ISaxoTokenService _saxoTokenService = saxoTokenService;
		private readonly IPortfolioJobRunner _jobRunner = jobRunner;
		private readonly SchedulerStatusStore _statusStore = statusStore;

		protected override async Task ExecuteAsync(CancellationToken ct)
		{
			_logger.LogInformation("╔═══════════════════════════════════════════╗");
			_logger.LogInformation("║  STOCKPRIZE WORKER STARTET                ║");
			_logger.LogInformation("╚═══════════════════════════════════════════╝");

			_logger.LogInformation("[STARTUP] Performing initial token refresh...");
			await _saxoTokenService.GetAccessTokenAsync(ct);
			_logger.LogInformation("[STARTUP] ✓ Initial token refresh completed");

			while (!ct.IsCancellationRequested)
			{
				try
				{
					var nextRunUtc = GetNextRunTime(3, 30);
					while (nextRunUtc.DayOfWeek == DayOfWeek.Sunday || nextRunUtc.DayOfWeek == DayOfWeek.Monday)
					{
						nextRunUtc = nextRunUtc.AddDays(1);
					}

					_statusStore.SetNextRunAt(nextRunUtc);
					_logger.LogInformation("[SCHEDULER] Next run scheduled for: {nextRun:dd/MM/yyyy HH:mm} UTC (in {hours:F1} hours)",
						nextRunUtc, (nextRunUtc - DateTimeOffset.UtcNow).TotalHours);

					while (DateTimeOffset.UtcNow < nextRunUtc && !ct.IsCancellationRequested)
					{
						var timeUntilJob = nextRunUtc - DateTimeOffset.UtcNow;
						var refreshDelay = TimeSpan.FromMinutes(45);
						if (refreshDelay > timeUntilJob) break;

						_statusStore.SetNextTokenRefreshAt(DateTimeOffset.UtcNow.Add(refreshDelay));
						await Task.Delay(refreshDelay, ct);
						_statusStore.SetNextTokenRefreshAt(null);
						await _saxoTokenService.GetAccessTokenAsync(ct);
					}

					var finalDelay = nextRunUtc - DateTimeOffset.UtcNow;
					if (finalDelay > TimeSpan.Zero)
						await Task.Delay(finalDelay, ct);

					try
					{
						await _jobRunner.RunJobAsync(ct);
						_statusStore.SetLastRun(DateTimeOffset.UtcNow, true);
					}
					catch (Exception ex) when (ex is not OperationCanceledException)
					{
						_statusStore.SetLastRun(DateTimeOffset.UtcNow, false);
						throw;
					}
				}
				catch (OperationCanceledException)
				{
					_logger.LogInformation("[SCHEDULER] ✓ Worker stopped normally...");
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "[SCHEDULER] ✗ Unexpected error in scheduler!");
				}
			}
		}

		public DateTimeOffset GetNextRunTime(int hour, int minute)
		{
			var utcNow = DateTimeOffset.UtcNow;
			var nextRun = new DateTimeOffset(utcNow.Year, utcNow.Month, utcNow.Day, hour, minute, 0, TimeSpan.Zero);

			if (nextRun <= utcNow)
				nextRun = nextRun.AddDays(1);

			return nextRun;
		}
	}
}
