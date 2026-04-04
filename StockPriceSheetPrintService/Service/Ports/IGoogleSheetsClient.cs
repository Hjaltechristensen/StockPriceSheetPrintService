namespace StockPriceSheetPrintService.Service.Ports
{
	public interface IGoogleSheetsClient
	{
		Task<decimal> UpdateGoogleSheetsCellAsync(string spreadsheetId, string sheetName, string totalValue, CancellationToken ct);
		Task<List<(DateOnly Date, decimal Value)>> GetHistoricalDataAsync(string spreadsheetId, string sheetName, CancellationToken ct);
	}
}
