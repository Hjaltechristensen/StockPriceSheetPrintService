using StockPriceSheetPrintService.Service.Ports.Inbound;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Service.Application
{
	public class SaxoLoginServiceImpl(ISaxoAuthService saxoAuthService, IDiscordNotifier discordNotifier) : ISaxoLoginService
	{
		public async Task<string> GetLoginUrlAsync(ClientContext ctx, CancellationToken ct)
		{
			var loginUrl = await saxoAuthService.BuildLoginUrl();
			await discordNotifier.SendLoginUrlAsync(loginUrl, ct);
			return loginUrl;
		}
	}
}
