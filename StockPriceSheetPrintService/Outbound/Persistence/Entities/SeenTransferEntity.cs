namespace StockPriceSheetPrintService.Outbound.Persistence.Entities
{
	public class SeenTransferEntity
	{
		public string BookingId { get; set; } = "";
		public DateTime SeenAt { get; set; }
	}
}
