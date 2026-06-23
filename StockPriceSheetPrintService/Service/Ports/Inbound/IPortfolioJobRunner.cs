namespace StockPriceSheetPrintService.Service.Ports.Inbound
{
	public interface IPortfolioJobRunner
	{
		Task RunJobAsync(ClientContext ctx, CancellationToken ct, bool sendDiscordImmediately = false);
	}
}
