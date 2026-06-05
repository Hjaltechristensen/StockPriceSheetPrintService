using StockPriceSheetPrintService.OutboundDto;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IHtmlScraper
	{
		Task<JuneData?> GetJuneNavAsync(string url, CancellationToken token);
		Task<JuneData?> GetFromYahooApiAsync(string ticker, CancellationToken token);
	}
}
