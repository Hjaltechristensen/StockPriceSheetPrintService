using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IMarketStackService
	{
		Task<EodResponse?> GetStockPricesAsync(CancellationToken ct);
	}
}
