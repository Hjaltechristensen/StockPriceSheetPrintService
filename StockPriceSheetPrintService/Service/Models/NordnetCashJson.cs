namespace StockPriceSheetPrintService.Service.Models
{
	public class NordnetCashJson
	{
		public decimal CashAmount { get; set; } = 0m;
		public DateTime LastUpdated { get; set; } = DateTime.Now;
	}
}
