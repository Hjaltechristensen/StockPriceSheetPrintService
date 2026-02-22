using Microsoft.Extensions.Options;
using StockPrizeSenderService.GoogleSheets;
using StockPrizeSenderService.Models;
using StockPrizeSenderService.TestData;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace StockPrizeSenderService
{
	public class StockprizeWorker : BackgroundService
	{
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly ILogger<StockprizeWorker> _logger;
		private readonly HtmlScraper _htmlScraper;
		private readonly UpdateCellAsync _updateCellAsync;
		private readonly TestDataClass _testDataClass;
		private readonly IConfiguration _configuration;
		private readonly string _executionLogPath = "execution_log.txt";
		private const int MaxExecutionsPerHour = 3;
		private const int MaxExecutionsPerMonth = 100;

		// Cache for JSON serialization
		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true
		};

		// In-memory cache for execution timestamps
		private readonly ConcurrentBag<DateTimeOffset> _executionCache = new();
		private DateTimeOffset _lastFileSyncTime = DateTimeOffset.UtcNow;

		public StockprizeWorker(IHttpClientFactory httpClientFactory, ILogger<StockprizeWorker> logger, IConfiguration configuration, HtmlScraper htmlScraper, UpdateCellAsync updateCellAsync, TestDataClass testDataClass)
		{
			_httpClientFactory = httpClientFactory;
			_logger = logger;
			_htmlScraper = htmlScraper;
			_updateCellAsync = updateCellAsync;
			_testDataClass = testDataClass;
			_configuration = configuration;

			// Load initial execution history from file
			LoadExecutionHistoryFromFile();
		}

		private void Test()
		{
			string? appKey = _configuration["Saxo:AppKey"];
			string redirectUrl = "http://localhost:5151/saxo/callback"; // Skal matche portalen 100%
			string authUrl = $"https://live.saxobank.com/sim/openapi/controls/oauth/authorize?client_id={appKey}&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUrl)}";

			_logger.LogInformation("OAuth login URL genereret - se nedenfor:");
			Console.WriteLine("Venligst log ind her for at give adgang:");
			Console.WriteLine(authUrl);
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			Test();
			while (!stoppingToken.IsCancellationRequested)
			{
				decimal totalValue = 0;
				var localTime = GetLocalTime(DateTimeOffset.UtcNow);
				var nextRunUtc = GetNextRunTime(localTime, 03, 30);
				var delay = nextRunUtc - DateTimeOffset.UtcNow;

				if (delay < TimeSpan.Zero)
					delay = TimeSpan.Zero;

				_logger.LogInformation("Næste kørsel planlagt til: {nextRun}", nextRunUtc);

				try
				{
					await Task.Delay(delay, stoppingToken);

					if (!IsExecutionSafe())
					{
						_logger.LogWarning("Kørsel blokeret: For mange kørsler registreret. Sikkerhedsmekanisme aktiveret.");
						continue;
					}
					EodResponse eodResponse;
					if (DateTime.UtcNow >= new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc))
					{
						eodResponse = await GetStockPrizeAsync(stoppingToken);
					}
					else
					{
						eodResponse = _testDataClass.Test();
					}

					if (eodResponse != null)
					{
						totalValue = CalculateTotalStockValueAsync(eodResponse);
					}

					totalValue += await FindTotalFundValue(stoppingToken);
					var sheetsKey = _configuration["SheetsApi:SheetsKey"];
					if (sheetsKey == null)
					{
						_logger.LogError("SheetsKey mangler i konfigurationen. Kørsel afbrudt.");
						continue;
					}
					await _updateCellAsync.UpdateGoogleSheetsCellAsync(sheetsKey, "Ark1", totalValue);

					LogExecution();
				}
				catch (OperationCanceledException)
				{
					_logger.LogInformation("Worker stopper...");
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Fejl i daglig opgave");
				}
			}
		}

		private decimal CalculateTotalStockValueAsync(EodResponse data)
		{
			decimal totalPrice = 0;
			if (data == null)
			{
				return 0;
			}
			data.Data.ForEach(d =>
			{
				if (AllTickers.Symbols.TryGetValue(d.Symbol, out decimal multiplier))
				{
					Console.WriteLine($"{multiplier} x {d.Symbol} closed at: {d.Close} total: {multiplier * d.Close}");
					totalPrice += (decimal)d.Close * multiplier;
				}
			});
			return totalPrice;
		}


		private async Task<EodResponse?> GetStockPrizeAsync(CancellationToken stoppingToken)
		{
			var client = _httpClientFactory.CreateClient("StockApi");

			var symbolsQuery = string.Join(",", AllTickers.Symbols.Keys);

			var response = await client.GetAsync($"v2/eod/latest?access_key={_configuration["StockApi:AccessKey"]}&symbols={symbolsQuery}", stoppingToken);
			response.EnsureSuccessStatusCode();

			var json = await response.Content.ReadAsStringAsync(stoppingToken);

			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			};

			var eodResponse = JsonSerializer.Deserialize<EodResponse>(json, options);
			return eodResponse;
		}

		private async Task<decimal> FindTotalFundValue(CancellationToken stoppingToken)
		{
			decimal shareAmount = 708.4689m;
			decimal totalFundValue = 0m;

			var fundPrice = await _htmlScraper.GetFundNavAsync("https://www.danskeinvest.dk/w/show_funds.product?p_nId=75&p_nFundgroup=75&p_nFund=5847", stoppingToken);
			if (fundPrice != null)
			{
				_logger.LogInformation("Dagens NAV: {nav} pr. {date}", fundPrice.Nav, fundPrice.Date.ToString("dd/MM/yyyy"));
				totalFundValue = fundPrice.Nav * shareAmount;
			}
			return totalFundValue;
		}

		private DateTimeOffset GetNextRunTime(DateTimeOffset currentTime, int hour, int minute)
		{
			var localNow = GetLocalTime(currentTime);

			var nextRunDate = new DateTime(
				localNow.Year, localNow.Month, localNow.Day,
				hour, minute, 0, DateTimeKind.Unspecified);

			if (nextRunDate <= localNow.DateTime)
			{
				nextRunDate = nextRunDate.AddDays(1);
			}

			var offset = GetUtcOffset(nextRunDate);
			var nextRun = new DateTimeOffset(nextRunDate, offset);

			return nextRun;
		}


		private DateTimeOffset GetLocalTime(DateTimeOffset utcTime)
		{
			var offset = GetUtcOffset(utcTime.DateTime);
			return utcTime.ToOffset(offset);
		}

		private TimeSpan GetUtcOffset(DateTime dateTime)
		{
			int year = dateTime.Year;

			var marchLastDay = new DateTime(year, 3, 31);
			var dstStart = marchLastDay.AddDays(-(int)marchLastDay.DayOfWeek);
			dstStart = dstStart.AddHours(2);

			var octoberLastDay = new DateTime(year, 10, 31);
			var dstEnd = octoberLastDay.AddDays(-(int)octoberLastDay.DayOfWeek);
			dstEnd = dstEnd.AddHours(3);

			if (dateTime >= dstStart && dateTime < dstEnd)
			{
				return TimeSpan.FromHours(2);
			}
			else
			{
				return TimeSpan.FromHours(1);
			}
		}

		private bool IsExecutionSafe()
		{
			try
			{
				if (!File.Exists(_executionLogPath))
					return true;

				var lines = File.ReadAllLines(_executionLogPath);
				var now = DateTimeOffset.UtcNow;

				var recentExecutions = lines
					.Where(line => DateTimeOffset.TryParse(line, out var timestamp))
					.Select(line => DateTimeOffset.Parse(line))
					.ToList();

				var executionsThisMonth = recentExecutions
					.Count(ts => ts.Year == now.Year && ts.Month == now.Month);

				var executionsLastHour = recentExecutions
					.Count(ts => (now - ts).TotalHours < 1);

				if (executionsLastHour >= MaxExecutionsPerHour)
				{
					_logger.LogWarning("Sikkerhedsadvarsel: {count} kørsler på 1 time. Grænse: {limit}",
						executionsLastHour, MaxExecutionsPerHour);
					return false;
				}

				if (executionsThisMonth >= MaxExecutionsPerMonth)
				{
					_logger.LogWarning("Sikkerhedsadvarsel: {count} kørsler denne måned. Grænse: {limit}",
						executionsThisMonth, MaxExecutionsPerMonth);
					return false;
				}

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Fejl ved sikkerhedstjek");
				return true;
			}
		}

		private void LogExecution()
		{
			try
			{
				var now = DateTimeOffset.UtcNow;
				_executionCache.Add(now);

				// Sync to file periodically (every 10 executions or every 1 hour)
				if ((now - _lastFileSyncTime).TotalHours > 1 || _executionCache.Count % 10 == 0)
				{
					SyncExecutionCacheToFile();
					_lastFileSyncTime = now;
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Fejl ved logning af kørsel");
			}
		}

		private void LoadExecutionHistoryFromFile()
		{
			try
			{
				if (!File.Exists(_executionLogPath))
					return;

				var lines = File.ReadAllLines(_executionLogPath);
				var now = DateTimeOffset.UtcNow;

				foreach (var line in lines)
				{
					if (DateTimeOffset.TryParse(line, out var timestamp) &&
						(now - timestamp).TotalDays < 40)
					{
						_executionCache.Add(timestamp);
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Fejl ved indlæsning af kørselshistorik");
			}
		}

		private void SyncExecutionCacheToFile()
		{
			try
			{
				var now = DateTimeOffset.UtcNow;

				// Get recent items and sort them
				var recentExecutions = _executionCache
					.Where(ts => (now - ts).TotalDays < 40)
					.OrderBy(ts => ts)
					.Select(ts => ts.ToString("O"))
					.ToList();

				if (recentExecutions.Any())
				{
					File.WriteAllLines(_executionLogPath, recentExecutions);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Fejl ved synkronisering af kørselslog");
			}
		}
	}
}