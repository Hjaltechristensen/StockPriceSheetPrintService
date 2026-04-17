namespace StockPriceSheetPrintService.Service.Ports.Inbound
{
	public interface ISaxoLoginService
	{
		Task<string> GetLoginUrlAsync(CancellationToken ct);
	}
}
