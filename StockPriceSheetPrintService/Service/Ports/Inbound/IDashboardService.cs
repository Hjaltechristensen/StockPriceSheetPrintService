namespace StockPriceSheetPrintService.Service.Ports.Inbound
{
	public interface IDashboardService
	{
		Task<List<(DateOnly Date, decimal Value)>> GetHistoricalDataAsync(CancellationToken ct);
	}
}
