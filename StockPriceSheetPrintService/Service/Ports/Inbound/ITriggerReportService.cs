namespace StockPriceSheetPrintService.Service.Ports.Inbound
{
	public interface ITriggerReportService
	{
		Task<bool> TrySendPendingReportAsync(CancellationToken ct);
	}
}
