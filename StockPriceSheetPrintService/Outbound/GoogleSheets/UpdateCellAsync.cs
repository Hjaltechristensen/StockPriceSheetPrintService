using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using StockPriceSheetPrintService.Service.Ports.Outbound;
using System.Globalization;

namespace StockPriceSheetPrintService.Outbound.GoogleSheets
{
	public class GoogleSheetsClientImpl(ILogger<GoogleSheetsClientImpl> logger) : IGoogleSheetsClient
	{
		private readonly ILogger<GoogleSheetsClientImpl> _logger = logger;

		private const string DateColumn = "A";
		private const string ValueColumn = "B";
		private const string CredentialsPath = "Secrets/stockprizeservice-59bc4ea3961d.json";
		private const string ApplicationName = "HomeServerBackend";

		private async Task<SheetsService> CreateServiceAsync(CancellationToken ct)
		{
			var credential = (await CredentialFactory.FromFileAsync<ServiceAccountCredential>(CredentialsPath, ct))
				.ToGoogleCredential()
				.CreateScoped(SheetsService.Scope.Spreadsheets);

			return new SheetsService(new BaseClientService.Initializer
			{
				HttpClientInitializer = credential,
				ApplicationName = ApplicationName
			});
		}

		public async Task<List<(DateOnly Date, decimal Value)>> GetHistoricalDataAsync(string spreadsheetId, string sheetName, CancellationToken ct)
		{
			var service = await CreateServiceAsync(ct);

			var getRequest = service.Spreadsheets.Values.Get(spreadsheetId, $"'{sheetName}'!{DateColumn}:{ValueColumn}");
			getRequest.ValueRenderOption = SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.UNFORMATTEDVALUE;
			var response = await getRequest.ExecuteAsync(ct);
			var rows = response.Values ?? [];

			// Google Sheets epoch: dage siden 30. december 1899
			var sheetsEpoch = new DateOnly(1899, 12, 30);

			var result = new List<(DateOnly, decimal)>();
			foreach (var row in rows)
			{
				if (row.Count < 2) continue;
				var dateStr = row[0]?.ToString();
				var valueStr = row[1]?.ToString();
				if (dateStr is null || valueStr is null) continue;

				// Med UNFORMATTED_VALUE returneres datoer som serienumre (double)
				if (!DateOnly.TryParseExact(dateStr, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateOnly date))
				{
					if (!double.TryParse(dateStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var serial))
						continue;
					date = sheetsEpoch.AddDays((int)serial);
				}

				if (!decimal.TryParse(valueStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value)) continue;
				result.Add((date, value));
			}
			return result;
		}

		public async Task<decimal> UpdateGoogleSheetsCellAsync(string spreadsheetId, string sheetName, string totalValue, CancellationToken ct)
		{
			var service = await CreateServiceAsync(ct);

			// Hent hele kolonnen og find næste tomme række
			var getRequest = service.Spreadsheets.Values.Get(spreadsheetId, $"'{sheetName}'!{DateColumn}:{ValueColumn}");
			var getResponse = await getRequest.ExecuteAsync(ct);
			var existingValues = getResponse.Values ?? [];

			var lastRow = existingValues.LastOrDefault();
			var rawValue = lastRow?[1]?.ToString();

			var dayBeforeValue = 0m;
			if (rawValue is not null)
			{
				// Fjern "kr " præfiks og eventuelle mellemrum
				var cleanValue = rawValue.Replace("kr", "").Replace(" ", "").Trim();

				// Håndter dansk formatering: punktum som tusindtalsseparator, komma som decimalseparator
				cleanValue = cleanValue.Replace(".", "").Replace(",", ".");

				dayBeforeValue = decimal.Parse(cleanValue, NumberStyles.Any, CultureInfo.InvariantCulture);
			}
			int nextRow = existingValues.Count + 1;
			_logger.LogInformation("[SHEETS] Writing to row {row}", nextRow);

			var updateRange = $"'{sheetName}'!{DateColumn}{nextRow}:{ValueColumn}{nextRow}";
			var valueRange = new ValueRange
			{
				Values =
				[
					[
						DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("dd/MM/yyyy"),
						totalValue
					]
				]
			};

			var updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, updateRange);
			updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
			await updateRequest.ExecuteAsync(ct);

			_logger.LogInformation("[SHEETS] ✓ Value {value} written to {range}", totalValue, updateRange);
			return dayBeforeValue;
		}
	}
}