namespace StockPriceSheetPrintService.Service.Exceptions
{
	public class NordnetStoreException(string message, Exception innerException) : Exception(message, innerException)
	{ }
}
