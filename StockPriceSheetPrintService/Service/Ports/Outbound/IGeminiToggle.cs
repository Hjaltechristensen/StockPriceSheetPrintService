namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IGeminiToggle
	{
		Task<bool> IsEnabledAsync();
		Task ToggleAsync();
	}
}
