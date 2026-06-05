using StockPriceSheetPrintService.OutboundDto.Saxo.InstrumentDetails;
using StockPriceSheetPrintService.OutboundDto.Saxo.Transactions;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IGeminiReportInsights
	{
		Task<string?> GetInsightsAsync(decimal saxoBalance, decimal nordnetValue, decimal juneValue, decimal total, decimal previousDayValue, List<SaxoTransaction> newTransfers, List<string> nordnetTickers, List<SaxoInstrument> saxoPositions, CancellationToken ct);
	}
}
