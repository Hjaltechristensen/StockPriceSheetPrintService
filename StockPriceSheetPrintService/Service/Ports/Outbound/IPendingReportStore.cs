using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IPendingReportStore
	{
		void Set(ScheduledReport report);
		ScheduledReport? Get();
		void Clear();
	}
}
