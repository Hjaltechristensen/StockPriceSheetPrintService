using System.Text.Json.Serialization;

namespace StockPriceSheetPrintService.Outbound.Dto
{
	public class EodDatum
	{
		public string Symbol { get; set; } = "";
		public string Exchange { get; set; } = "";
		public DateTimeOffset Date { get; set; }
		public decimal? Open { get; set; }
		public decimal? High { get; set; }
		public decimal? Low { get; set; }
		public decimal? Close { get; set; }
		public decimal? Volume { get; set; }
		[JsonPropertyName("price_currency")]
		public string PriceCurrency { get; set; } = string.Empty;
	}
}
