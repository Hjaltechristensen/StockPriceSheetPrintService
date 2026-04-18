using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface INordnetStore
	{
		Task<NordnetCashJson> GetCashAmountAsync();
		Task SetCashAmountAsync(decimal newAmount);
	}
}
