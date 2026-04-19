namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface ISchedulerStatus
	{
		DateTimeOffset? NextRunAt { get; }
		DateTimeOffset? NextTokenRefreshAt { get; }
		DateTimeOffset? LastRunAt { get; }
		bool? LastRunSucceeded { get; }
	}
}
