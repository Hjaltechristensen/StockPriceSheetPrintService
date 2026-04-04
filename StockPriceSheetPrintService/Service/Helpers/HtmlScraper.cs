using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports;

namespace StockPriceSheetPrintService.Service.Helpers
{
	public class HtmlScraper(NavProvider navProvider) : IHtmlScraper
	{
		public async Task<JuneData?> GetJuneNavAsync(string url, CancellationToken token)
		{
			return await navProvider.GetJuneNavAsync(url, token);
		}

		public async Task<JuneData?> GetFromYahooApiAsync(string ticker, CancellationToken token)
		{
			return await navProvider.GetFromYahooApiAsync(ticker, token);
		}
	}
}
