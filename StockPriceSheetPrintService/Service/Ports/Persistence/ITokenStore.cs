namespace StockPriceSheetPrintService.Service.Ports.Persistence
{
	public interface ITokenStore
	{
		Task SaveRefreshTokenAsync(string refreshToken, CancellationToken ct);
		Task<string?> ReadRefreshTokenAsync(CancellationToken ct);
		bool TokenExists();
	}
}
