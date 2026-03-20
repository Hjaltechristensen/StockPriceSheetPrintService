namespace StockPriceSheetPrintService.Service.Ports
{
	public interface IPortfolioJobRunner
	{
		Task RunJobAsync(CancellationToken ct, bool sendDiscordImmediately = false);
	}
}
