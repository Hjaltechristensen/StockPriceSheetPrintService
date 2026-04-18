using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports.Outbound;
using StockPriceSheetPrintService.Service.Ports.Persistence;

namespace StockPriceSheetPrintService.Service.Application
{
	public class PortfolioDataFetcher(
		ILogger<PortfolioDataFetcher> logger,
		IConfiguration configuration,
		ISaxoTokenService saxoTokenService,
		ISaxoAccountService saxoAccountService,
		IMarketStackService marketStackService,
		IGoogleSheetsClient googleSheetsClient,
		IPortfolioCalculator portfolioCalculator,
		ISeenTransferStore seenTransferStore,
		INordnetStore nordnetStore) : IPortfolioDataFetcher
	{
		private readonly ILogger<PortfolioDataFetcher> _logger = logger;
		private readonly IConfiguration _configuration = configuration;
		private readonly ISaxoTokenService _saxoTokenService = saxoTokenService;
		private readonly ISaxoAccountService _saxoAccountService = saxoAccountService;
		private readonly IMarketStackService _marketStackService = marketStackService;
		private readonly IGoogleSheetsClient _googleSheetsClient = googleSheetsClient;
		private readonly IPortfolioCalculator _portfolioCalculator = portfolioCalculator;
		private readonly ISeenTransferStore _seenTransferStore = seenTransferStore;
		private readonly INordnetStore _nordnetStore = nordnetStore;

		public async Task<decimal> GetSaxoBalanceAsync(CancellationToken ct)
		{
			try
			{
				var saxoToken = await _saxoTokenService.GetAccessTokenAsync(ct);
				if (saxoToken == null)
				{
					_logger.LogWarning("[FETCHER] Saxo token not available");
					return 0m;
				}

				var response = await _saxoAccountService.GetBalanceAsync(saxoToken, ct);
				if (response == null)
				{
					_logger.LogError("[FETCHER] Failed to get Saxo balance");
					return 0m;
				}

				_logger.LogInformation("[FETCHER] Saxo balance: {balance:F2} DKK", response.TotalValue);
				return response.TotalValue;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[FETCHER] Unexpected error fetching Saxo balance");
				return 0m;
			}
		}

		public async Task<decimal> GetNordnetValueAsync(CancellationToken ct)
		{
			try
			{
				var eodResponse = await _marketStackService.GetStockPricesAsync(ct);
				if (eodResponse == null)
				{
					_logger.LogError("[FETCHER] Failed to get stock prices");
					return 0m;
				}

				var stockValue = await _portfolioCalculator.CalculateTotalStockValueAsync(eodResponse, ct);
				var cash = await _nordnetStore.GetNordnetCashAmountAsync();
				var totalNordnetValue = stockValue + cash.CashAmount;

				_logger.LogInformation("[FETCHER] Nordnet value: {value:F2} DKK", totalNordnetValue);
				return totalNordnetValue;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[FETCHER] Unexpected error fetching Nordnet value");
				return 0m;
			}
		}

		public async Task<decimal> GetJuneValueAsync(CancellationToken ct)
		{
			try
			{
				var juneValue = await _portfolioCalculator.FindTotalJuneValueAsync(ct);
				if (juneValue == 0m)
				{
					_logger.LogError("[FETCHER] Failed to get June value");
					return 0m;
				}

				_logger.LogInformation("[FETCHER] June value: {value:F2} DKK", juneValue);
				return juneValue;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[FETCHER] Unexpected error fetching June value");
				return 0m;
			}
		}

		public async Task<List<SaxoTransaction>> GetNewTransfersAsync(CancellationToken ct)
		{
			try
			{
				var today = DateTime.UtcNow;
				if (today.Day < 28 && today.Day > 10)
				{
					_logger.LogDebug("[FETCHER] Not in transfer window, skipping transfer check");
					return [];
				}

				var saxoToken = await _saxoTokenService.GetAccessTokenAsync(ct);
				if (saxoToken == null)
				{
					_logger.LogWarning("[FETCHER] Saxo token not available for transfer check");
					return [];
				}

				var fromDate = DateTime.UtcNow.AddDays(-14);
				var toDate = DateTime.UtcNow;
				var response = await _saxoAccountService.GetSaxoTransactionsAsync(saxoToken, fromDate, toDate, ct);

				var seenIds = await _seenTransferStore.LoadAsync(ct);
				var newTransfers = response.Data
					.Where(t => !seenIds.Contains(t.BookingId))
					.ToList();

				if (newTransfers.Count > 0)
				{
					await _seenTransferStore.SaveAsync(newTransfers.Select(t => t.BookingId), ct);
					_logger.LogInformation("[FETCHER] Found {count} new transfers", newTransfers.Count);
				}

				return newTransfers;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[FETCHER] Unexpected error fetching transfers");
				return [];
			}
		}

		public async Task<decimal> GetPreviousDayValueAsync(CancellationToken ct)
		{
			try
			{
				var spreadsheetId = _configuration["SheetsApi:SheetsKey"];
				const string sheetName = "Daily";

				if (string.IsNullOrEmpty(spreadsheetId))
				{
					_logger.LogWarning("[FETCHER] SheetsApi:SheetsKey configuration missing, cannot fetch previous day value");
					return 0m;
				}

				var historicalData = await _googleSheetsClient.GetHistoricalDataAsync(spreadsheetId, sheetName, ct);

				if (historicalData.Count == 0)
				{
					_logger.LogWarning("[FETCHER] No historical data in Google Sheets");
					return 0m;
				}

				var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
				var previousDayEntry = historicalData.FirstOrDefault(x => x.Date == yesterday);

				if (previousDayEntry == default)
				{
					_logger.LogWarning("[FETCHER] No entry found for {date}, using latest available", yesterday);
					previousDayEntry = historicalData.OrderByDescending(x => x.Date).First();
				}

				_logger.LogInformation("[FETCHER] Previous day value ({date}): {value:F2} DKK", 
					previousDayEntry.Date, previousDayEntry.Value);
				return previousDayEntry.Value;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[FETCHER] Unexpected error fetching previous day value");
				return 0m;
			}
		}
	}
}
