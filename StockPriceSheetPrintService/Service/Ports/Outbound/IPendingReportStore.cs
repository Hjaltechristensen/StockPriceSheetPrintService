using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IPendingReportStore
	{
		void Set(PendingReport report);
		PendingReport? Get();
		void Clear();
	}
}
