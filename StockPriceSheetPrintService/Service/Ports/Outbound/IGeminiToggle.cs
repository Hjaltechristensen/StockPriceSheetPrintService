namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IGeminiToggle
	{
		bool IsEnabled { get; }
		void Toggle();
	}
}
