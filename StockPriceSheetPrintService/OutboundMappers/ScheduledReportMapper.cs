using StockPriceSheetPrintService.OutboundDto;
using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.OutboundMappers
{
	public static class ScheduledReportMapper
	{
		public static PendingReport ToDto(ScheduledReport domain) =>
			new(domain.SaxoBalance, domain.NordnetValue, domain.JuneValue,
				domain.Total, domain.PreviousDayValue, domain.TransferAmount,
				domain.GeminiInsights, domain.ScheduledAtUtc);

		public static ScheduledReport ToDomain(PendingReport dto) =>
			new(dto.SaxoBalance, dto.NordnetValue, dto.JuneValue,
				dto.Total, dto.PreviousDayValue, dto.TransferAmount,
				dto.GeminiInsights, dto.ScheduledAtUtc);
	}
}
