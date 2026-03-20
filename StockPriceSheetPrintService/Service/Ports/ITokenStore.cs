namespace StockPriceSheetPrintService.Service.Ports
{
	public interface ITokenStore
	{
		Task SaveRefreshTokenAsync(string refreshToken, CancellationToken ct);
		Task<string?> ReadRefreshTokenAsync(CancellationToken ct);
		bool TokenExists();
	}
}
