namespace StockPriceSheetPrintService.Service.Models.Saxo.Transactions
{
	public class SaxoTransaction
	{
		public string BookingId { get; set; } = string.Empty;
		public decimal BookedAmount { get; set; }
		public decimal IntradayAmount { get; set; }
		public bool IsIntradayData { get; set; }

		public decimal Amount => BookedAmount != 0 ? BookedAmount : IntradayAmount;
	}
}
