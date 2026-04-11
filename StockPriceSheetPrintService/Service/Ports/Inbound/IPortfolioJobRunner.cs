namespace StockPriceSheetPrintService.Service.Ports.Inbound
{
	public interface IPortfolioJobRunner
	{
		Task RunJobAsync(CancellationToken ct, bool sendDiscordImmediately = false);
	}
}
