namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IClaudeToggle
	{
		bool IsEnabled { get; }
		void Toggle();
	}
}
