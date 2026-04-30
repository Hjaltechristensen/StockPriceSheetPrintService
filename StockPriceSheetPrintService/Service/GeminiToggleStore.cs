using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Service
{
	public sealed class GeminiToggleStore : IGeminiToggle
	{
		public bool IsEnabled { get; private set; } = true;
		public void Toggle() => IsEnabled = !IsEnabled;
	}
}
