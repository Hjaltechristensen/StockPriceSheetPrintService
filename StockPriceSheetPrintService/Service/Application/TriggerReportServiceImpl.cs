using StockPriceSheetPrintService.Service.Ports.Inbound;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Service.Application
{
	public class TriggerReportServiceImpl(IPendingReportStore pendingReportStore, IDiscordNotifier discordNotifier) : ITriggerReportService
	{
		private readonly IPendingReportStore _pendingReportStore = pendingReportStore;
		private readonly IDiscordNotifier _discordNotifier = discordNotifier;

		public async Task<bool> TrySendPendingReportAsync(CancellationToken ct)
		{
			var report = _pendingReportStore.Get();
			if (report is null) return false;

			await _discordNotifier.SendMorningReportAsync(
				report.SaxoBalance, report.NordnetValue, report.JuneValue,
				report.Total, report.PreviousDayValue, report.TransferAmount,
				report.GeminiInsights, ct);

			_pendingReportStore.Clear();
			return true;
		}
	}
}
