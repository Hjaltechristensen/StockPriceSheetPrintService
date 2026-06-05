namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface ITokenStore
	{
		Task SaveRefreshTokenAsync(string refreshToken, CancellationToken ct);
		Task<string?> ReadRefreshTokenAsync(CancellationToken ct);
		bool TokenExists();
	}
}
