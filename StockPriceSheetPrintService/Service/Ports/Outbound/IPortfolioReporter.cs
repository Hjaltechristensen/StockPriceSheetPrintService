using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IPortfolioReporter
	{
		Task ReportMorningAsync(decimal saxoBalance, decimal nordnetValue, decimal juneValue, decimal total, decimal previousDayValue, List<Transfer> newTransfers, bool sendDiscordImmediately, string? geminiInsights, ClientContext ctx, CancellationToken ct);
		Task UpdateGoogleSheetsAsync(decimal total, ClientContext ctx, CancellationToken ct);
	}
}
