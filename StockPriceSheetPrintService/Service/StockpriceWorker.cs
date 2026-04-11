using StockPriceSheetPrintService.Service.Ports;
using StockPriceSheetPrintService.Service.Ports.Inbound;

namespace StockPriceSheetPrintService.Service
{
	public class StockpriceWorker(
		ILogger<StockpriceWorker> logger,
		ISaxoTokenService saxoTokenService,
		IPortfolioJobRunner jobRunner) : BackgroundService
	{
		private readonly ILogger<StockpriceWorker> _logger = logger;
		private readonly ISaxoTokenService _saxoTokenService = saxoTokenService;
		private readonly IPortfolioJobRunner _jobRunner = jobRunner;

		protected override async Task ExecuteAsync(CancellationToken ct)
		{
			_logger.LogInformation("╔═══════════════════════════════════════════╗");
			_logger.LogInformation("║  STOCKPRIZE WORKER STARTET                ║");
			_logger.LogInformation("╚═══════════════════════════════════════════╝");

			_logger.LogInformation("[STARTUP] Udfører initial token refresh...");
			await _saxoTokenService.GetAccessTokenAsync(ct);
			_logger.LogInformation("[STARTUP] ✓ Initial token refresh gennemført");

			while (!ct.IsCancellationRequested)
			{
				try
				{
					var utcNow = DateTimeOffset.UtcNow;
					var nextRunUtc = GetNextRunTime(3, 30);

					while (nextRunUtc.DayOfWeek == DayOfWeek.Sunday || nextRunUtc.DayOfWeek == DayOfWeek.Monday)
					{
						nextRunUtc = nextRunUtc.AddDays(1);
					}

					var delay = nextRunUtc - utcNow;
					if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

					_logger.LogInformation("[SCHEDULER] Næste kørsel planlagt til: {nextRun:dd/MM/yyyy HH:mm} UTC (om {hours:F1} timer)",
						nextRunUtc, delay.TotalHours);

					while (DateTimeOffset.UtcNow < nextRunUtc && !ct.IsCancellationRequested)
					{
						var timeUntilJob = nextRunUtc - DateTimeOffset.UtcNow;
						var refreshDelay = TimeSpan.FromMinutes(45);

						if (refreshDelay > timeUntilJob) break;

						_logger.LogInformation("[SCHEDULER] Session refresh om 45 min for at holde token i live...");
						await Task.Delay(refreshDelay, ct);
						_logger.LogInformation("[SCHEDULER] Udfører token refresh...");
						await _saxoTokenService.GetAccessTokenAsync(ct);
					}

					var finalDelay = nextRunUtc - DateTimeOffset.UtcNow;
					if (finalDelay > TimeSpan.Zero)
						await Task.Delay(finalDelay, ct);

					await _jobRunner.RunJobAsync(ct);
				}
				catch (OperationCanceledException)
				{
					_logger.LogInformation("[SCHEDULER] ✓ Worker stopper normalt...");
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "[SCHEDULER] ✗ UVENTET FEJL i scheduleren!");
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
