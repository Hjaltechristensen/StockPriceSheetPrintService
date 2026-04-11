using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IPortfolioReporter
	{
		Task ReportMorningAsync(decimal saxoBalance, decimal nordnetValue, decimal juneValue, decimal total, decimal previousDayValue, List<SaxoTransaction> newTransfers, bool sendDiscordImmediately, CancellationToken ct);
		Task UpdateGoogleSheetsAsync(decimal total, CancellationToken ct);
	}
}
