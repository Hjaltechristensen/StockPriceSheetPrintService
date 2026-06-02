using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Outbound.Filesystem
{
	public class InMemoryPendingReportStore : IPendingReportStore
	{
		private PendingReport? _current;
		public void Set(PendingReport report) => _current = report;
		public PendingReport? Get() => _current;
		public void Clear() => _current = null;
	}
}
