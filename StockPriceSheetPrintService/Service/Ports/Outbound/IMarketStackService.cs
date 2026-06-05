using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IMarketStackService
	{
		Task<List<StockPrice>?> GetStockPricesAsync(CancellationToken ct);
	}
}
