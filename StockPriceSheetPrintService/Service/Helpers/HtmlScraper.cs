using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports;

namespace StockPriceSheetPrintService.Service.Helpers
{
	public class HtmlScraper(FundNavClient fundNavClient) : IFundPriceProvider
	{
		public async Task<FundPrice?> GetFundNavAsync(string url, CancellationToken token)
		{
			return await fundNavClient.GetFundNavAsync(url, token);
		}

		public async Task<FundPrice?> GetFromYahooApiAsync(string ticker, CancellationToken token)
		{
			return await fundNavClient.GetFromYahooApiAsync(ticker, token);
		}
	}
}
