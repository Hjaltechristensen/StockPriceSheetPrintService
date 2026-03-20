using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports
{
	public interface ISaxoAuthService
	{
		Task<SaxoTokenResult> ExchangeCodeForTokensAsync(string code, CancellationToken ct);
		Task<SaxoBalanceResponse> GetBalanceAsync(string accessToken, CancellationToken ct);
		string BuildLoginUrl();
	}
}
