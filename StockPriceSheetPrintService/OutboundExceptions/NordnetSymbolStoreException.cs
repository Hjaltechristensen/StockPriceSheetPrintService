namespace StockPriceSheetPrintService.OutboundExceptions
{
	public class NordnetSymbolStoreException(string message, Exception? inner = null)
		: Exception(message, inner);
}
