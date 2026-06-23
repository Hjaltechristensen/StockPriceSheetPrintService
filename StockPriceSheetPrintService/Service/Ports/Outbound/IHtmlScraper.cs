using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IHtmlScraper
	{
		Task<FundNav?> GetJuneNavAsync(string url, ClientContext ctx, CancellationToken token);
		Task<FundNav?> GetFromYahooApiAsync(string ticker, ClientContext ctx, CancellationToken token);
	}
}
