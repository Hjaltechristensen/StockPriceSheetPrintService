using StockPriceSheetPrintService.Krypto;
using StockPrizeSenderService.GoogleSheets;
using StockPrizeSenderService.Models;
using StockPrizeSenderService.TestData;
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

		// ✅ FIX: JsonOptions genbruges nu korrekt
		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true
		};

		// ✅ FIX: Erstattet ConcurrentBag med en trådsikker liste + lock
		private readonly List<DateTimeOffset> _executionCache = new();
		private readonly object _cacheLock = new();
		private DateTimeOffset _lastFileSyncTime = DateTimeOffset.UtcNow;

		public StockprizeWorker(IHttpClientFactory httpClientFactory, ILogger<StockprizeWorker> logger, IConfiguration configuration, HtmlScraper htmlScraper, UpdateCellAsync updateCellAsync, TestDataClass testDataClass)
		{
			_httpClientFactory = httpClientFactory;
			_logger = logger;
			_htmlScraper = htmlScraper;
			_updateCellAsync = updateCellAsync;
			_testDataClass = testDataClass;
			_configuration = configuration;

			LoadExecutionHistoryFromFile();
		}

		// ✅ FIX: ManualLogin kaldes kun hvis token-filen ikke eksisterer
		private void ManualLoginIfNeeded()
		{
			string tokenPath = "/app/data/refresh_token.bin";
			if (File.Exists(tokenPath))
			{
				_logger.LogInformation("Refresh token fundet – springer manuel login over.");
				return;
			}

			string? appKey = _configuration["Saxo:AppKey"];
			string redirectUrl = _configuration["Saxo:RedirectUrl"] ?? "http://localhost:5151/saxo/callback";
			string authEndpoint = _configuration["Saxo:AuthEndpoint"] ?? "https://live.logonvalidation.net/authorize";
			string authUrl = $"{authEndpoint}?client_id={appKey}&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUrl)}";

			_logger.LogWarning("Ingen refresh token fundet – manuel login kræves.");
			Console.WriteLine("\n************************************************************");
			Console.WriteLine("KOPIÉR DETTE LINK TIL DIN BROWSER FOR AT GIVE ADGANG (LIVE):");
			Console.WriteLine(authUrl);
			Console.WriteLine("************************************************************\n");
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			ManualLoginIfNeeded();

			while (!stoppingToken.IsCancellationRequested)
			{
				var localTime = GetLocalTime(DateTimeOffset.UtcNow);
				var nextRunUtc = GetNextRunTime(localTime, 03, 30);
				var delay = nextRunUtc - DateTimeOffset.UtcNow;

				if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

				_logger.LogInformation("Næste kørsel planlagt til: {nextRun}", nextRunUtc);

				try
				{
					await Task.Delay(delay, stoppingToken);
					await RunJobAsync(stoppingToken);

				}
				catch (OperationCanceledException)
				{
					_logger.LogInformation("Worker stopper...");
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Fejl i den daglige kørsel.");
				}
			}
		}

		public async Task RunJobAsync(CancellationToken stoppingToken)
		{
			if (!IsExecutionSafe())
			{
				_logger.LogWarning("Kørsel blokeret: Sikkerhedsmekanisme aktiveret.");
				return;
			}

			_logger.LogInformation("Starter daglig værdiberegning...");
decimal nordnetCash = 1116m;
			decimal runningTotal = nordnetCash;

			// --- 1. SAXO BALANCE ---
			string? saxoToken = await GetSaxoAccessTokenAsync(stoppingToken);
			if (saxoToken != null)
			{
				decimal saxoBalance = await GetSaxoBalanceAsync(saxoToken, stoppingToken);
				runningTotal += saxoBalance;
				_logger.LogInformation("Saxo balance hentet: {val} DKK", saxoBalance);
			}
			else
			{
				_logger.LogWarning("Kunne ikke hente Saxo balance - fortsætter med de øvrige værdier.");
			}

			// --- 2. AKTIER (MarketStack) ---
			EodResponse? eodResponse;
			DateTime liveStockDate = DateTime.Parse(_configuration["StockApi:LiveFromDate"] ?? "2026-03-01");
			if (DateTime.UtcNow >= liveStockDate)
			{
				eodResponse = await GetStockPricesAsync(stoppingToken);
			}
			else
			{
				_logger.LogInformation("Bruger testdata for aktier (før {date}).", liveStockDate.ToString("dd/MM/yyyy"));
				eodResponse = _testDataClass.Test();
			}

			if (eodResponse != null)
			{
				decimal stockValue = CalculateTotalStockValue(eodResponse);
				runningTotal += stockValue;
				_logger.LogInformation("Aktieværdi beregnet: {val} DKK", stockValue);
			}

			// --- 3. FONDE (Scraper) ---
			decimal fundValue = await FindTotalFundValue(stoppingToken);
			runningTotal += fundValue;
			_logger.LogInformation("Fondsværdi hentet: {val} DKK", fundValue);

			// --- 4. OPDATER GOOGLE SHEETS ---
			var sheetsKey = _configuration["SheetsApi:SheetsKey"];
			if (!string.IsNullOrEmpty(sheetsKey))
			{
				_logger.LogInformation("Sender total værdi til Google Sheets: {total} DKK", runningTotal);
				await _updateCellAsync.UpdateGoogleSheetsCellAsync(sheetsKey, "Ark1", runningTotal);
				LogExecution();
			}
			else
			{
				_logger.LogError("SheetsKey mangler! Kunne ikke gemme resultatet.");
			}
		}

		private async Task<string?> GetSaxoAccessTokenAsync(CancellationToken stoppingToken)
		{
			string tokenPath = "/app/data/refresh_token.bin";
			string? encryptionKey = _configuration["Saxo:EncryptionKey"];

			if (!File.Exists(tokenPath)) return null;

			try
			{
				string encryptedToken = await File.ReadAllTextAsync(tokenPath, stoppingToken);
				string refreshToken = TokenEncryptor.Decrypt(encryptedToken, encryptionKey!);

				var client = _httpClientFactory.CreateClient();
				var requestData = new FormUrlEncodedContent(new Dictionary<string, string>
				{
					{ "grant_type", "refresh_token" },
					{ "refresh_token", refreshToken },
					{ "client_id", _configuration["Saxo:AppKey"] },
					{ "client_secret", _configuration["Saxo:AppSecret"] }
				});

				string tokenEndpoint = "https://live.logonvalidation.net/token";
				var response = await client.PostAsync(tokenEndpoint, requestData, stoppingToken);
				var errorBody = await response.Content.ReadAsStringAsync(stoppingToken);

_logger.LogError("Saxo refresh fejl. Status: {Status}. Body: {Body}",
    response.StatusCode,
    errorBody);
				if (!response.IsSuccessStatusCode)
				{
					_logger.LogError("Saxo LIVE afviste refresh token.");
					return null;
				}

				var json = await response.Content.ReadAsStringAsync(stoppingToken);
				// ✅ FIX: Genbruger den statiske JsonOptions
				using var doc = JsonDocument.Parse(json);

				string newAccessToken = doc.RootElement.GetProperty("access_token").GetString()!;
				_logger.LogError(newAccessToken);
				string newRefreshToken = doc.RootElement.GetProperty("refresh_token").GetString()!;
				_logger.LogError(newRefreshToken);

				string encryptedNewToken = TokenEncryptor.Encrypt(newRefreshToken, encryptionKey!);
				await File.WriteAllTextAsync(tokenPath, encryptedNewToken, stoppingToken);

				return newAccessToken;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Fejl under Saxo token refresh.");
				return null;
			}
		}

		private async Task<decimal> GetSaxoBalanceAsync(string accessToken, CancellationToken stoppingToken)
		{
			var client = _httpClientFactory.CreateClient();
			client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

			string apiEndpoint = "https://gateway.saxobank.com/openapi/port/v1/balances/me";
			var response = await client.GetAsync(apiEndpoint, stoppingToken);

			if (response.IsSuccessStatusCode)
			{
				var json = await response.Content.ReadAsStringAsync(stoppingToken);
				using var doc = JsonDocument.Parse(json);
				return doc.RootElement.GetProperty("TotalValue").GetDecimal();
			}

			return 0;
		}

		// ✅ FIX: Omdøbt fra CalculateTotalStockValueAsync – metoden er ikke async
		private decimal CalculateTotalStockValue(EodResponse data)
		{
			decimal totalPrice = 0;
			if (data == null) return 0;

			data.Data.ForEach(d =>
			{
				if (AllTickers.Symbols.TryGetValue(d.Symbol, out decimal multiplier))
				{
					Console.WriteLine($"{multiplier} x {d.Symbol} closed at: {d.Close} total: {multiplier * d.Close}");
					totalPrice += (decimal)d.Close * multiplier;
				}
			});

			Console.WriteLine($"Total stock value from Nordnet: {totalPrice}");

			return totalPrice;
		}

		// ✅ FIX: Omdøbt fra GetStockPrizeAsync – "Prize" → "Prices"
		private async Task<EodResponse?> GetStockPricesAsync(CancellationToken stoppingToken)
		{
			var client = _httpClientFactory.CreateClient("StockApi");
			var symbolsQuery = string.Join(",", AllTickers.Symbols.Keys);

			var response = await client.GetAsync($"v2/eod/latest?access_key={_configuration["StockApi:AccessKey"]}&symbols={symbolsQuery}", stoppingToken);
			response.EnsureSuccessStatusCode();

			var json = await response.Content.ReadAsStringAsync(stoppingToken);

			// ✅ FIX: Genbruger den statiske JsonOptions fremfor at instantiere en ny
			return JsonSerializer.Deserialize<EodResponse>(json, JsonOptions);
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
				nextRunDate = nextRunDate.AddDays(1);

			var offset = GetUtcOffset(nextRunDate);
			return new DateTimeOffset(nextRunDate, offset);
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
			var dstStart = marchLastDay.AddDays(-(int)marchLastDay.DayOfWeek).AddHours(2);

			var octoberLastDay = new DateTime(year, 10, 31);
			var dstEnd = octoberLastDay.AddDays(-(int)octoberLastDay.DayOfWeek).AddHours(3);

			return (dateTime >= dstStart && dateTime < dstEnd)
				? TimeSpan.FromHours(2)
				: TimeSpan.FromHours(1);
		}

		// ✅ FIX: IsExecutionSafe bruger nu _executionCache i stedet for at læse fra fil
		private bool IsExecutionSafe()
		{
			try
			{
				var now = DateTimeOffset.UtcNow;

				lock (_cacheLock)
				{
					var executionsThisMonth = _executionCache
						.Count(ts => ts.Year == now.Year && ts.Month == now.Month);

					var executionsLastHour = _executionCache
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
				}

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Fejl ved sikkerhedstjek");
				return true;
			}
		}

		// ✅ FIX: LogExecution bruger nu en tidsstyret sync fremfor upålidelig % 10
		private void LogExecution()
		{
			try
			{
				var now = DateTimeOffset.UtcNow;

				lock (_cacheLock)
				{
					_executionCache.Add(now);
				}

				if ((now - _lastFileSyncTime).TotalHours > 1)
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
				if (!File.Exists(_executionLogPath)) return;

				var lines = File.ReadAllLines(_executionLogPath);
				var now = DateTimeOffset.UtcNow;

				lock (_cacheLock)
				{
					foreach (var line in lines)
					{
						if (DateTimeOffset.TryParse(line, out var timestamp) &&
							(now - timestamp).TotalDays < 40)
						{
							_executionCache.Add(timestamp);
						}
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
				List<string> recentExecutions;

				lock (_cacheLock)
				{
					recentExecutions = _executionCache
						.Where(ts => (now - ts).TotalDays < 40)
						.OrderBy(ts => ts)
						.Select(ts => ts.ToString("O"))
						.ToList();
				}

				if (recentExecutions.Any())
					File.WriteAllLines(_executionLogPath, recentExecutions);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Fejl ved synkronisering af kørselslog");
			}
		}
	}
}
