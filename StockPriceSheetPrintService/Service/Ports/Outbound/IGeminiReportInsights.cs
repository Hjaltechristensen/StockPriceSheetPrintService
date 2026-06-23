using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IGeminiReportInsights
	{
		Task<string?> GetInsightsAsync(
			decimal saxoBalance, decimal nordnetValue, decimal juneValue,
			decimal total, decimal previousDayValue,
			List<Transfer> newTransfers,
			List<string> nordnetTickers,
			List<Instrument> saxoPositions,
			ClientContext ctx,
			CancellationToken ct);
	}
}
