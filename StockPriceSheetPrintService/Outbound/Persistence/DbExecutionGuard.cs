using Microsoft.EntityFrameworkCore;
using StockPriceSheetPrintService.Outbound.Persistence.Entities;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Outbound.Persistence
{
	public class DbExecutionGuard(IDbContextFactory<StockDbContext> dbFactory, ILogger<DbExecutionGuard> logger) : IExecutionGuard
	{
		private const int MaxExecutionsPerHour = 3;
		private const int MaxExecutionsPerMonth = 100;

		public bool IsExecutionSafe()
		{
			try
			{
				using var db = dbFactory.CreateDbContext();
				var now = DateTimeOffset.UtcNow;
				var hourAgo = now.AddHours(-1);
				var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

				var thisHour = db.ExecutionLogs.Count(e => e.ExecutedAt >= hourAgo);
				var thisMonth = db.ExecutionLogs.Count(e => e.ExecutedAt >= monthStart);

				if (thisHour >= MaxExecutionsPerHour)
				{
					logger.LogWarning("Safety warning: {count} executions in 1 hour. Limit: {limit}", thisHour, MaxExecutionsPerHour);
					return false;
				}

				if (thisMonth >= MaxExecutionsPerMonth)
				{
					logger.LogWarning("Safety warning: {count} executions this month. Limit: {limit}", thisMonth, MaxExecutionsPerMonth);
					return false;
				}

				return true;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error during safety check");
				return true;
			}
		}

		public void LogExecution()
		{
			try
			{
				using var db = dbFactory.CreateDbContext();
				db.ExecutionLogs.Add(new ExecutionLogEntity { ExecutedAt = DateTimeOffset.UtcNow });
				db.SaveChanges();

				var cutoff = DateTimeOffset.UtcNow.AddDays(-40);
				db.ExecutionLogs.Where(e => e.ExecutedAt < cutoff).ExecuteDelete();
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error during execution logging");
			}
		}
	}
}
