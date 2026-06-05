using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IJuneStore
	{
		Task<FundHolding> GetJuneSharesAmountAsync();
		Task SetJuneSharesAmountAsync(decimal amount);
	}
}
