namespace StockPriceSheetPrintService.Service.Ports.Inbound
{
	public interface ISaxoLoginService
	{
		Task<string> GetLoginUrlAsync(ClientContext ctx, CancellationToken ct);
	}
}
