namespace StockPriceSheetPrintService.Outbound.Persistence.Entities
{
	public class NordnetSymbolEntity
	{
		public string Ticker { get; set; } = "";
		public decimal Shares { get; set; }
	}
}
