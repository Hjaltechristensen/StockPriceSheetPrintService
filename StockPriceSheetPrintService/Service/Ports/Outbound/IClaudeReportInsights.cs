using Discord;
using StockPriceSheetPrintService.Service.Models.Saxo.Transactions;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IClaudeReportInsights
	{
		Task<string?> GetInsightsAsync(decimal saxoBalance, decimal nordnetValue, decimal juneValue, decimal total, decimal previousDayValue, List<SaxoTransaction> newTransfers, List<string> tickers, CancellationToken ct);
	}
}
