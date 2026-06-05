using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface INordnetStore
	{
		Task<CashBalance> GetNordnetCashAmountAsync();
		Task SetNordnetCashAmountAsync(decimal newAmount);
	}
}
