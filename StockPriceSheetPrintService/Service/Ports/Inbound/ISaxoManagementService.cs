namespace StockPriceSheetPrintService.Service.Ports.Inbound
{
	public record SaxoCallbackResult(decimal TotalValue, string Currency);

	public interface ISaxoManagementService
	{
		Task<SaxoCallbackResult> HandleCallbackAsync(string code, ClientContext ctx, CancellationToken ct);
		Task<string?> GetOrRefreshAccessTokenAsync(ClientContext ctx, CancellationToken ct);
	}
}
