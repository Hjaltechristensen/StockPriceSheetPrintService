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
		public async Task<SaxoCallbackResult> HandleCallbackAsync(string code, ClientContext ctx, CancellationToken ct)
		{
			var tokens = await saxoAuthService.ExchangeCodeForTokensAsync(code, ctx, ct);
			await tokenStore.SaveRefreshTokenAsync(tokens.RefreshToken, ct);
			var balance = await saxoAccountService.GetBalanceAsync(tokens.AccessToken, ctx, ct);
			return new SaxoCallbackResult(balance?.TotalValue ?? 0m, balance?.Currency ?? string.Empty);
		}

		public Task<string?> GetOrRefreshAccessTokenAsync(ClientContext ctx, CancellationToken ct) =>
			saxoTokenService.GetAccessTokenAsync(ctx, ct);
	}
}
