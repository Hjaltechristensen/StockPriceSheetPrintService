namespace StockPriceSheetPrintService.Service.Exceptions
{
	public class NordnetSymbolStoreException(string message, Exception? inner = null)
	: Exception(message, inner);
}
