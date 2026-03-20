namespace StockPriceSheetPrintService.Service.Ports
{
	public interface ISaxoTokenService
	{
		Task<string?> GetAccessTokenAsync(CancellationToken ct);
	}
}
