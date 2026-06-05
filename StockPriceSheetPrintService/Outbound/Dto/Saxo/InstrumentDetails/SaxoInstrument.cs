using System.Text.Json.Serialization;

namespace StockPriceSheetPrintService.Outbound.Dto.Saxo.InstrumentDetails
{
	public class SaxoInstrument
	{
		public int Uic { get; set; }

		[JsonPropertyName("Description")]
		public string Description { get; set; } = string.Empty;

		[JsonPropertyName("Symbol")]
		public string Symbol { get; set; } = string.Empty;

		[JsonPropertyName("AssetType")]
		public string AssetType { get; set; } = string.Empty;

		[JsonPropertyName("CurrencyCode")]
		public string CurrencyCode { get; set; } = string.Empty;

		[JsonPropertyName("Exchange")]
		public ExchangeDto Exchange { get; set; } = default!;
	}
}
