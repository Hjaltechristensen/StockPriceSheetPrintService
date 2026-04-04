namespace StockPriceSheetPrintService.Service.Ports
{
	public interface IDiscordNotifier
	{
		Task SendMorningReportAsync(decimal saxoBalance, decimal stockValue, decimal juneValue, decimal total, decimal dayBeforeValue, CancellationToken stoppingToken);
	}
}
