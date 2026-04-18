namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface INordnetSymbolStore
	{
		Task<Dictionary<string, decimal>> GetSymbolsAsync();
		Task AddOrUpdateSymbolAsync(string ticker, decimal shares);
		Task RemoveSymbolAsync(string ticker);
	}
}
