using StockPriceSheetPrintService.OutboundDto.Saxo.InstrumentDetails;
using StockPriceSheetPrintService.OutboundDto.Saxo.Transactions;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IPortfolioDataFetcher
	{
		Task<decimal> GetSaxoBalanceAsync(CancellationToken ct);
		Task<decimal> GetNordnetValueAsync(CancellationToken ct);
		Task<decimal> GetJuneValueAsync(CancellationToken ct);
		Task<List<SaxoTransaction>> GetNewTransfersAsync(CancellationToken ct);
		Task<decimal> GetPreviousDayValueAsync(CancellationToken ct);
		Task<List<SaxoInstrument>> GetNetPositionsAsync(CancellationToken ct);
	}
}
