using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IHtmlScraper
	{
		Task<FundNav?> GetJuneNavAsync(string url, CancellationToken token);
		Task<FundNav?> GetFromYahooApiAsync(string ticker, CancellationToken token);
	}
}
