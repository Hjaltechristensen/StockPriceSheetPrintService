using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports
{
	public interface IFundPriceProvider
	{
		Task<FundPrice?> GetFundNavAsync(string url, CancellationToken token);
	}
}
