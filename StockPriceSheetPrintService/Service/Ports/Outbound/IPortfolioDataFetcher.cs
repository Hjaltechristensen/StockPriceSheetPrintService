using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IPortfolioDataFetcher
	{
		Task<decimal> GetSaxoBalanceAsync(CancellationToken ct);
		Task<decimal> GetNordnetValueAsync(CancellationToken ct);
		Task<decimal> GetJuneValueAsync(CancellationToken ct);
		Task<List<Transfer>> GetNewTransfersAsync(CancellationToken ct);
		Task<decimal> GetPreviousDayValueAsync(CancellationToken ct);
		Task<List<Instrument>> GetNetPositionsAsync(CancellationToken ct);
	}
}
