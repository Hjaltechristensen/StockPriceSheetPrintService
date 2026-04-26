using StockPriceSheetPrintService.Service.Models.Saxo.Transactions;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IPortfolioDataFetcher
	{
		Task<decimal> GetSaxoBalanceAsync(CancellationToken ct);
		Task<decimal> GetNordnetValueAsync(CancellationToken ct);
		Task<decimal> GetJuneValueAsync(CancellationToken ct);
		Task<List<SaxoTransaction>> GetNewTransfersAsync(CancellationToken ct);
		Task<decimal> GetPreviousDayValueAsync(CancellationToken ct);
	}
}
