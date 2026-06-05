using StockPriceSheetPrintService.Outbound.Dto;
using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Outbound.Mappers
{
	public static class JuneMapper
	{
		public static FundNav ToFundNav(JuneData dto) =>
			new() { Nav = dto.Nav, Date = dto.Date };

		public static FundHolding ToFundHolding(JuneAmountData dto) =>
			new(dto.Amount, dto.LastUpdated);
	}
}
