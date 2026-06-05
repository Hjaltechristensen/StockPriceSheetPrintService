using StockPriceSheetPrintService.OutboundDto.Saxo.InstrumentDetails;
using StockPriceSheetPrintService.OutboundDto.Saxo.Transactions;

namespace StockPriceSheetPrintService.OutboundMappers
{
	public static class SaxoMapper
	{
		public static IEnumerable<string> ToBookingIds(IEnumerable<SaxoTransaction> transactions) =>
			transactions.Select(t => t.BookingId);

		public static IEnumerable<string> ToTickers(IEnumerable<SaxoInstrument> instruments) =>
			instruments.Select(i => i.Symbol);
	}
}
