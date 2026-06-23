using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface ISaxoAuthService
	{
		Task<OAuthTokens> ExchangeCodeForTokensAsync(string code, ClientContext ctx, CancellationToken ct);
		Task<string> BuildLoginUrl();
	}
}
