using StockPriceSheetPrintService.OutboundDto;
using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.OutboundMappers
{
	public static class NordnetMapper
	{
		public static CashBalance ToCashBalance(NordnetCashJson dto) =>
			new(dto.CashAmount, dto.LastUpdated);
	}
}
