using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IPortfolioDataFetcher
	{
		Task<decimal> GetSaxoBalanceAsync(ClientContext ctx, CancellationToken ct);
		Task<decimal> GetNordnetValueAsync(ClientContext ctx, CancellationToken ct);
		Task<decimal> GetJuneValueAsync(ClientContext ctx, CancellationToken ct);
		Task<List<Transfer>> GetNewTransfersAsync(ClientContext ctx, CancellationToken ct);
		Task<decimal> GetPreviousDayValueAsync(ClientContext ctx, CancellationToken ct);
		Task<List<Instrument>> GetNetPositionsAsync(ClientContext ctx, CancellationToken ct);
		Task<string> GetAtmValue(ClientContext ctx, CancellationToken ct);
	}
}
