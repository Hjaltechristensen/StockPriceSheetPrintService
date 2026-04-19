using StockPriceSheetPrintService.Service.Ports.Inbound;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Service
{
	public class StockpriceWorker(
		ILogger<StockpriceWorker> logger,
		ISaxoTokenService saxoTokenService,
		IPortfolioJobRunner jobRunner) : BackgroundService, ISchedulerStatus
	{
		private readonly ILogger<StockpriceWorker> _logger = logger;
		private readonly ISaxoTokenService _saxoTokenService = saxoTokenService;
		private readonly IPortfolioJobRunner _jobRunner = jobRunner;

		private DateTimeOffset? _nextRunAt;
		private DateTimeOffset? _nextTokenRefreshAt;
		private DateTimeOffset? _lastRunAt;
		private bool? _lastRunSucceeded;

		// ISchedulerStatus
		public DateTimeOffset? NextRunAt => _nextRunAt;
		public DateTimeOffset? NextTokenRefreshAt => _nextTokenRefreshAt;
		public DateTimeOffset? LastRunAt => _lastRunAt;
		public bool? LastRunSucceeded => _lastRunSucceeded;


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
						nextRunUtc = nextRunUtc.AddDays(1);

					_nextRunAt = nextRunUtc;

					while (DateTimeOffset.UtcNow < nextRunUtc && !ct.IsCancellationRequested)
					{
						var timeUntilJob = nextRunUtc - DateTimeOffset.UtcNow;
						var refreshDelay = TimeSpan.FromMinutes(45);
						if (refreshDelay > timeUntilJob) break;

						_nextTokenRefreshAt = DateTimeOffset.UtcNow.Add(refreshDelay);
						await Task.Delay(refreshDelay, ct);
						_nextTokenRefreshAt = null;
						await _saxoTokenService.GetAccessTokenAsync(ct);
					}

					var finalDelay = nextRunUtc - DateTimeOffset.UtcNow;
					if (finalDelay > TimeSpan.Zero)
						await Task.Delay(finalDelay, ct);

					try
					{
						await _jobRunner.RunJobAsync(ct);
						_lastRunAt = DateTimeOffset.UtcNow;
						_lastRunSucceeded = true;
					}
					catch
					{
						_lastRunAt = DateTimeOffset.UtcNow;
						_lastRunSucceeded = false;
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
