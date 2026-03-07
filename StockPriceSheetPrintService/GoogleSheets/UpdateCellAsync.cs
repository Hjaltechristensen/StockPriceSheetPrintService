using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

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

        public async Task UpdateGoogleSheetsCellAsync(string spreadsheetId, string sheetName, string totalValue)
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
            var getRequest = service.Spreadsheets.Values.Get(spreadsheetId, $"'{sheetName}'!{DateColumn}:{DateColumn}");
            var getResponse = await getRequest.ExecuteAsync();
            var existingValues = getResponse.Values ?? new List<IList<object>>();

            int nextRow = existingValues.Count + 1;
            _logger.LogInformation("[SHEETS] Skriver til række {row}", nextRow);

            var updateRange = $"'{sheetName}'!{DateColumn}{nextRow}:{ValueColumn}{nextRow}";
            var valueRange = new ValueRange
            {
                Values = new List<IList<object>>
                {
                    new List<object>
                    {
                        DateOnly.FromDateTime(DateTime.Now.AddDays(-1)).ToString("dd/MM/yyyy"),
                        totalValue
                    }
                }
            };

            var updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, updateRange);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await updateRequest.ExecuteAsync();

            _logger.LogInformation("[SHEETS] ✓ Værdi {value} skrevet til {range}", totalValue, updateRange);
        }
    }
}