namespace StockPriceSheetPrintService.Outbound.Persistence.Entities
{
	public class ExecutionLogEntity
	{
		public int Id { get; set; }
		public DateTimeOffset ExecutedAt { get; set; }
	}
}
