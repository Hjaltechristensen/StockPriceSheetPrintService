namespace StockPriceSheetPrintService.Service.Models
{
	public class StockPrice
	{
		public string Symbol { get; set; } = string.Empty;
		public string Exchange { get; set; } = string.Empty;
		public DateTimeOffset Date { get; set; }
		public decimal? Close { get; set; }
		public string Currency { get; set; } = string.Empty;
	}
}
