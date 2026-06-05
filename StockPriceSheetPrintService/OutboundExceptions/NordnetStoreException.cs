namespace StockPriceSheetPrintService.OutboundExceptions
{
	public class NordnetStoreException(string message, Exception innerException) : Exception(message, innerException)
	{ }
}
