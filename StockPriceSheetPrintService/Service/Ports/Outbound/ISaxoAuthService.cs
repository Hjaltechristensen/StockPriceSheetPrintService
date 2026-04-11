using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface ISaxoAuthService
	{
		Task<SaxoTokenResult> ExchangeCodeForTokensAsync(string code, CancellationToken ct);
		Task<string> BuildLoginUrl();
	}
}
