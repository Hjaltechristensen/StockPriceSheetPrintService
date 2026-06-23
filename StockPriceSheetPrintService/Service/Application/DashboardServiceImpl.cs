using StockPriceSheetPrintService.Service.Ports.Inbound;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Service.Application
{
	public class DashboardServiceImpl(IGoogleSheetsClient googleSheetsClient, IConfiguration configuration) : IDashboardService
	{
		public Task<List<(DateOnly Date, decimal Value)>> GetHistoricalDataAsync(ClientContext ctx, CancellationToken ct)
		{
			var spreadsheetId = configuration["SheetsApi:SheetsKey"]
				?? throw new InvalidOperationException("SheetsApi:SheetsKey is not configured");
			return googleSheetsClient.GetHistoricalDataAsync(spreadsheetId, "Daily", ctx, ct);
		}
	}
}
