namespace StockPriceSheetPrintService
{
	public record ClientContext(Guid CorrelationId, string Source, DateTimeOffset InitiatedAt);
}
