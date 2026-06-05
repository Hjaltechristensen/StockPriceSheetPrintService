using StockPriceSheetPrintService.Outbound.Dto;
using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Outbound.Mappers
{
	public static class NordnetMapper
	{
		public static CashBalance ToCashBalance(NordnetCashJson dto) =>
			new(dto.CashAmount, dto.LastUpdated);
	}
}
