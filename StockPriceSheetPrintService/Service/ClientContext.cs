namespace StockPriceSheetPrintService.Service
{
	public record ClientContext(Guid CorrelationId, string Source, DateTimeOffset InitiatedAt);
}
