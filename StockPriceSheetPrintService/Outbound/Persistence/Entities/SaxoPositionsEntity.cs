using StockPriceSheetPrintService.Service.Models.Saxo.InstrumentDetails;

namespace StockPriceSheetPrintService.Outbound.Persistence.Entities
{
	public class SaxoPositionsEntity
	{
		public int Uic { get; set; }
		public string AssetType { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public string Symbol { get; set; } = string.Empty;
		public string CurrencyCode { get; set; } = string.Empty;
		public ExchangeDto Exchange { get; set; } = default!;
	}
}
