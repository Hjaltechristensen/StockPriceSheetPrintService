using Serilog.Context;
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

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation("╔═══════════════════════════════════════════╗");
			_logger.LogInformation("║  STOCKPRIZE WORKER STARTET                ║");
			_logger.LogInformation("╚═══════════════════════════════════════════╝");

			await PerformStartupTokenRefreshAsync(stoppingToken);

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					var nextRunUtc = GetNextScheduledRunTime();
					_statusStore.SetNextRunAt(nextRunUtc);
					_logger.LogInformation("[SCHEDULER] Next run scheduled for: {nextRun:dd/MM/yyyy HH:mm} UTC (in {hours:F1} hours)",
						nextRunUtc, (nextRunUtc - DateTimeOffset.UtcNow).TotalHours);

					await WaitUntilNextRunAsync(nextRunUtc, stoppingToken);
					await RunScheduledJobAsync(stoppingToken);
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

		private async Task PerformStartupTokenRefreshAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation("[STARTUP] Performing initial token refresh...");
			var startupCtx = ClientContextFactory.New("Startup:TokenRefresh");
			using (LogContext.PushProperty("CorrelationId", startupCtx.CorrelationId))
			using (LogContext.PushProperty("Source", startupCtx.Source))
			{
				await _saxoTokenService.GetAccessTokenAsync(startupCtx, stoppingToken);
			}
			_logger.LogInformation("[STARTUP] ✓ Initial token refresh completed");
		}

		private DateTimeOffset GetNextScheduledRunTime()
		{
			var nextRunUtc = GetNextRunTime(3, 30);
			while (nextRunUtc.DayOfWeek == DayOfWeek.Sunday || nextRunUtc.DayOfWeek == DayOfWeek.Monday)
			{
				nextRunUtc = nextRunUtc.AddDays(1);
			}
			return nextRunUtc;
		}

		private async Task WaitUntilNextRunAsync(DateTimeOffset nextRunUtc, CancellationToken stoppingToken)
		{
			while (DateTimeOffset.UtcNow < nextRunUtc && !stoppingToken.IsCancellationRequested)
			{
				var timeUntilJob = nextRunUtc - DateTimeOffset.UtcNow;
				var refreshDelay = TimeSpan.FromMinutes(45);
				if (refreshDelay > timeUntilJob) break;

				_statusStore.SetNextTokenRefreshAt(DateTimeOffset.UtcNow.Add(refreshDelay));
				await Task.Delay(refreshDelay, stoppingToken);
				_statusStore.SetNextTokenRefreshAt(null);
				var refreshCtx = ClientContextFactory.New("Scheduler:TokenRefresh");
				using (LogContext.PushProperty("CorrelationId", refreshCtx.CorrelationId))
				using (LogContext.PushProperty("Source", refreshCtx.Source))
				{
					await _saxoTokenService.GetAccessTokenAsync(refreshCtx, stoppingToken);
				}
			}

			var finalDelay = nextRunUtc - DateTimeOffset.UtcNow;
			if (finalDelay > TimeSpan.Zero)
				await Task.Delay(finalDelay, stoppingToken);
		}

		private async Task RunScheduledJobAsync(CancellationToken stoppingToken)
		{
			try
			{
				var jobCtx = ClientContextFactory.New("Scheduler:Job");
				using (LogContext.PushProperty("CorrelationId", jobCtx.CorrelationId))
				using (LogContext.PushProperty("Source", jobCtx.Source))
				{
					await _jobRunner.RunJobAsync(jobCtx, stoppingToken);
				}
				_statusStore.SetLastRun(DateTimeOffset.UtcNow, true);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				_statusStore.SetLastRun(DateTimeOffset.UtcNow, false);
				throw;
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
