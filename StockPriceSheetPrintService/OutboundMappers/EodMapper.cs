using StockPriceSheetPrintService.OutboundDto;

namespace StockPriceSheetPrintService.OutboundMappers
{
	public static class EodMapper
	{
		public static IEnumerable<(string Symbol, string Exchange, decimal? Close, string PriceCurrency)> ToSymbolPrices(EodResponse response) =>
			response.Data.Select(d => (d.Symbol, d.Exchange, d.Close, d.PriceCurrency));
	}
}
