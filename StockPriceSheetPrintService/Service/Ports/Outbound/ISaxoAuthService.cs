using StockPriceSheetPrintService.OutboundDto.Saxo;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface ISaxoAuthService
	{
		Task<SaxoTokenResult> ExchangeCodeForTokensAsync(string code, CancellationToken ct);
		Task<string> BuildLoginUrl();
	}
}
