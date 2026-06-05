using StockPriceSheetPrintService.Outbound.Dto;
using StockPriceSheetPrintService.Outbound.Mappers;
using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Outbound.Memory
{
	public class InMemoryPendingReportStore : IPendingReportStore
	{
		private PendingReport? _current;

		public void Set(ScheduledReport report) => _current = ScheduledReportMapper.ToDto(report);
		public ScheduledReport? Get() => _current is null ? null : ScheduledReportMapper.ToDomain(_current);
		public void Clear() => _current = null;
	}
}
