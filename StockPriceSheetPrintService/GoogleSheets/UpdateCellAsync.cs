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

		public UpdateCellAsync(IConfiguration configuration, ILogger<UpdateCellAsync> logger)
		{
			_configuration = configuration;
			_logger = logger;
		}

		public async Task UpdateGoogleSheetsCellAsync(string spreadsheetId, string sheetName, decimal totalValue)
		{
				var credential = GoogleCredential
				.FromFile("Secrets/stockprizeservice-59bc4ea3961d.json")
					.CreateScoped(SheetsService.Scope.Spreadsheets);

				var service = new SheetsService(new BaseClientService.Initializer
				{
					HttpClientInitializer = credential,
					ApplicationName = "HomeServerBackend"
				});

				var valueRange = new ValueRange
				{
					Values = new List<IList<object>>
						{
							new List<object>
							{
								DateOnly.FromDateTime(DateTime.Now).ToString("dd/MM/yyyy"),
								totalValue
							}
						}
				};

				var request = service.Spreadsheets.Values.Append(
					valueRange,
					spreadsheetId,
					$"'{sheetName}'!R15"
				);

				request.ValueInputOption =
					SpreadsheetsResource.ValuesResource.AppendRequest
						.ValueInputOptionEnum.USERENTERED;

				await request.ExecuteAsync();
		}
	}
}
