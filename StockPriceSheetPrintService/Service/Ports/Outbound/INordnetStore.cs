using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface INordnetStore
	{
		Task<NordnetCashJson> GetNordnetCashAmountAsync();
		Task SetNordnetCashAmountAsync(decimal newAmount);
	}
}
