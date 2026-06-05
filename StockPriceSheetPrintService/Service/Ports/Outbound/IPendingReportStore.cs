using StockPriceSheetPrintService.OutboundDto;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IPendingReportStore
	{
		void Set(PendingReport report);
		PendingReport? Get();
		void Clear();
	}
}
