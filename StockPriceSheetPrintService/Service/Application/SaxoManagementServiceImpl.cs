using StockPriceSheetPrintService.Service.Ports.Inbound;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Service.Application
{
	public class SaxoManagementServiceImpl(
		ISaxoAuthService saxoAuthService,
		ISaxoAccountService saxoAccountService,
		ITokenStore tokenStore,
		ISaxoTokenService saxoTokenService) : ISaxoManagementService
	{
		public async Task<SaxoCallbackResult> HandleCallbackAsync(string code, CancellationToken ct)
		{
			var tokens = await saxoAuthService.ExchangeCodeForTokensAsync(code, ct);
			await tokenStore.SaveRefreshTokenAsync(tokens.RefreshToken, ct);
			var balance = await saxoAccountService.GetBalanceAsync(tokens.AccessToken, ct);
			return new SaxoCallbackResult(balance?.TotalValue ?? 0m, balance?.Currency ?? string.Empty);
		}

		public Task<string?> GetOrRefreshAccessTokenAsync(CancellationToken ct) =>
			saxoTokenService.GetAccessTokenAsync(ct);
	}
}
