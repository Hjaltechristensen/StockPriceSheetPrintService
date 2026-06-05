using StockPriceSheetPrintService.OutboundDto;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IPortfolioCalculator
	{
		Task<decimal> CalculateTotalStockValueAsync(EodResponse data, CancellationToken ct);
		Task<decimal> FindTotalJuneValueAsync(CancellationToken ct);
	}
}
