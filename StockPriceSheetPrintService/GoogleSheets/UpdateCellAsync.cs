using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System.Globalization;

namespace StockPrizeSenderService.GoogleSheets
{
    public class UpdateCellAsync
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<UpdateCellAsync> _logger;

        private const string DateColumn = "A";
        private const string ValueColumn = "B";
        private const string CredentialsPath = "Secrets/stockprizeservice-59bc4ea3961d.json";
        private const string ApplicationName = "HomeServerBackend";

        public UpdateCellAsync(IConfiguration configuration, ILogger<UpdateCellAsync> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<decimal> UpdateGoogleSheetsCellAsync(string spreadsheetId, string sheetName, string totalValue)
        {
            var credential = GoogleCredential
                .FromFile(CredentialsPath)
                .CreateScoped(SheetsService.Scope.Spreadsheets);

            var service = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName
            });

			// Hent hele kolonnen og find næste tomme række
			var getRequest = service.Spreadsheets.Values.Get(spreadsheetId, $"'{sheetName}'!{DateColumn}:{ValueColumn}");
			var getResponse = await getRequest.ExecuteAsync();
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
			_logger.LogInformation("[SHEETS] Skriver til række {row}", nextRow);

            var updateRange = $"'{sheetName}'!{DateColumn}{nextRow}:{ValueColumn}{nextRow}";
            var valueRange = new ValueRange
            {
                Values =
				[
					[
                        DateOnly.FromDateTime(DateTime.Now.AddDays(-1)).ToString("dd/MM/yyyy"),
                        totalValue
                    ]
                ]
            };

            var updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, updateRange);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await updateRequest.ExecuteAsync();

            _logger.LogInformation("[SHEETS] ✓ Værdi {value} skrevet til {range}", totalValue, updateRange);
            return dayBeforeValue;
		}
    }
}