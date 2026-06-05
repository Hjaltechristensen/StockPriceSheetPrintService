namespace StockPriceSheetPrintService.Service.Models
{
	public class Instrument
	{
		public int Uic { get; set; }
		public string Symbol { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public string AssetType { get; set; } = string.Empty;
		public string Currency { get; set; } = string.Empty;
		public string ExchangeId { get; set; } = string.Empty;
		public string ExchangeCountry { get; set; } = string.Empty;
		public string ExchangeName { get; set; } = string.Empty;
	}
}
