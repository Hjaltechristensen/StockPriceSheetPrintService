using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface ISaxoAuthService
	{
		Task<OAuthTokens> ExchangeCodeForTokensAsync(string code, CancellationToken ct);
		Task<string> BuildLoginUrl();
	}
}
