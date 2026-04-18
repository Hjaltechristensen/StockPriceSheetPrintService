using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IJuneStore
	{
		Task<JuneAmountData> GetJuneSharesAmount();
		Task SetJuneSharesAmount(decimal amount);
	}
}
