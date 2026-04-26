using System.Text.Json.Serialization;

namespace StockPriceSheetPrintService.Service.Models.Saxo.InstrumentDetails
{
	public class ExchangeDto
	{
		[JsonPropertyName("CountryCode")]
		public string CountryCode { get; set; } = string.Empty;

		[JsonPropertyName("ExchangeId")]
		public string ExchangeId { get; set; } = string.Empty;

		[JsonPropertyName("Name")]
		public string Name { get; set; } = string.Empty;
	}
}
