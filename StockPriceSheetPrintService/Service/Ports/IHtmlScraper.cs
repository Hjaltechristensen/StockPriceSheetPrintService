using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports
{
	public interface IHtmlScraper
	{
		Task<JuneData?> GetJuneNavAsync(string url, CancellationToken token);
		Task<JuneData?> GetFromYahooApiAsync(string ticker, CancellationToken token);
	}
}
