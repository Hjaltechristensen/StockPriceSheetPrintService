namespace StockPriceSheetPrintService.Service.Ports.Inbound
{
	public record SaxoCallbackResult(decimal TotalValue, string Currency);

	public interface ISaxoManagementService
	{
		Task<SaxoCallbackResult> HandleCallbackAsync(string code, CancellationToken ct);
		Task<string?> GetOrRefreshAccessTokenAsync(CancellationToken ct);
	}
}
