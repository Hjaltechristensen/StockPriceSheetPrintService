using StockPriceSheetPrintService.OutboundDto;
using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.OutboundMappers
{
	public static class EodMapper
	{
		public static StockPrice ToStockPrice(EodDatum dto) => new()
		{
			Symbol   = dto.Symbol,
			Exchange = dto.Exchange,
			Date     = dto.Date,
			Close    = dto.Close,
			Currency = dto.PriceCurrency,
		};

		public static List<StockPrice> ToStockPrices(EodResponse dto) =>
			dto.Data.Select(ToStockPrice).ToList();

		public static EodDatum ToDto(StockPrice domain) => new()
		{
			Symbol        = domain.Symbol,
			Exchange      = domain.Exchange,
			Date          = domain.Date,
			Close         = domain.Close,
			PriceCurrency = domain.Currency,
		};
	}
}
