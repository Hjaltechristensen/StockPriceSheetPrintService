namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface ISaxoTokenService
	{
		Task<string?> GetAccessTokenAsync(CancellationToken ct);
	}
}
