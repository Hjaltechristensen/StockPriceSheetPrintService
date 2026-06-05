using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Service.Application
{
	public sealed class SchedulerStatusStore : ISchedulerStatus
	{
		public DateTimeOffset? NextRunAt { get; private set; }
		public DateTimeOffset? NextTokenRefreshAt { get; private set; }
		public DateTimeOffset? LastRunAt { get; private set; }
		public bool? LastRunSucceeded { get; private set; }

		public void SetNextRunAt(DateTimeOffset value) => NextRunAt = value;
		public void SetNextTokenRefreshAt(DateTimeOffset? value) => NextTokenRefreshAt = value;
		public void SetLastRun(DateTimeOffset at, bool succeeded)
		{
			LastRunAt = at;
			LastRunSucceeded = succeeded;
		}
	}
}
