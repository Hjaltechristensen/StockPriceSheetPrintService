namespace StockPriceSheetPrintService.Service.Exceptions
{
	public class SaxoPositionStoreException(string message, Exception? inner = null)
	: Exception(message, inner);
}
