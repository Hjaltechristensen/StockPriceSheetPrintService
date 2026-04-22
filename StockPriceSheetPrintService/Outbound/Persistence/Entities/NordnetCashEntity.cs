namespace StockPriceSheetPrintService.Outbound.Persistence.Entities
{
	public class NordnetCashEntity
	{
		public int Id { get; set; }
		public decimal CashAmount { get; set; }
		public DateTime LastUpdated { get; set; }
	}
}
