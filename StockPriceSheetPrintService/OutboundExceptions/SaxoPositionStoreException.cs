namespace StockPriceSheetPrintService.OutboundExceptions
{
	public class SaxoPositionStoreException(string message, Exception? inner = null)
		: Exception(message, inner);
}
