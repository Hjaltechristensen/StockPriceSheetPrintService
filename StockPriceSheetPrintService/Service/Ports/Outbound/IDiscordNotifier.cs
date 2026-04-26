namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IDiscordNotifier
	{
		Task SendMorningReportAsync(decimal saxoBalance, decimal stockValue, decimal juneValue, decimal total, decimal dayBeforeValue, decimal? lastTransferAmount, string? claudeInsights, CancellationToken stoppingToken);
		Task SendLoginUrlAsync(string loginUrl, CancellationToken stoppingToken);
	}
}
