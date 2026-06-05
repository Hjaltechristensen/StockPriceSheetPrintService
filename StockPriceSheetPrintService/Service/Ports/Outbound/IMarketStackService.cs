using StockPriceSheetPrintService.OutboundDto;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IMarketStackService
	{
		Task<EodResponse?> GetStockPricesAsync(CancellationToken ct);
	}
}
