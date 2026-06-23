namespace StockPriceSheetPrintService.Service.Ports.Inbound
{
	public interface IDashboardService
	{
		Task<List<(DateOnly Date, decimal Value)>> GetHistoricalDataAsync(ClientContext ctx, CancellationToken ct);
	}
}
