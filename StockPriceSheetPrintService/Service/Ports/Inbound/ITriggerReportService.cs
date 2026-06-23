namespace StockPriceSheetPrintService.Service.Ports.Inbound
{
	public interface ITriggerReportService
	{
		Task<bool> TrySendPendingReportAsync(ClientContext ctx, CancellationToken ct);
	}
}
