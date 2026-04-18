using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IJuneStore
	{
		Task<JuneAmountData> GetJuneSharesAmountAsync();
		Task SetJuneSharesAmountAsync(decimal amount);
	}
}
