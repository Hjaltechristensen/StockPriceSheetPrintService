using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports
{
	public interface ISaxoService
	{
		Task<SaxoTokenResult> ExchangeCodeForTokensAsync(string code, CancellationToken ct);
		Task<SaxoBalanceResponse?> GetBalanceAsync(string accessToken, CancellationToken ct);
		Task<SaxoTransactionsResponse> GetSaxoTransactionsAsync(string accessToken, DateTime fromDate, DateTime toDate, CancellationToken ct);
		Task<string> BuildLoginUrl();
	}
}
