using Microsoft.Extensions.Options;
using StockPriceSheetPrintService.Krypto;
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

		private void ManualLogin()
		{
			// 1. Hent værdier fra din konfiguration (.env / docker-compose)
			string? appKey = _configuration["Saxo:AppKey"];

			// 2. BRUG LOCALHOST (Fordi du bruger SSH-tunnel og det matcher din .env/portal)
			string redirectUrl = _configuration["Saxo:RedirectUrl"] ?? "http://localhost:5151/saxo/callback";

			// 3. BRUG LIVE AUTH ENDPOINT (Ikke sim-openapi-controls...)
			string authEndpoint = _configuration["Saxo:AuthEndpoint"] ?? "https://live.logonvalidation.net/authorize";

			// 4. Byg URL'en med Uri.EscapeDataString
			string authUrl = $"{authEndpoint}?client_id={appKey}&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUrl)}";

			_logger.LogInformation("OAuth LIVE login URL genereret:");

			// Vi skriver det ud så det er nemt at kopiere fra SSH-terminalen
			Console.WriteLine("\n************************************************************");
			Console.WriteLine("KOPIÉR DETTE LINK TIL DIN BROWSER FOR AT GIVE ADGANG (LIVE):");
			Console.WriteLine(authUrl);
			Console.WriteLine("************************************************************\n");
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			// Print login-link ved opstart (kun til manuel brug hvis token udløber)
			ManualLogin();

			while (!stoppingToken.IsCancellationRequested)
			{
				var localTime = GetLocalTime(DateTimeOffset.UtcNow);
				var nextRunUtc = GetNextRunTime(localTime, 03, 30);
				var delay = nextRunUtc - DateTimeOffset.UtcNow;

				if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

				_logger.LogInformation("Næste kørsel planlagt til: {nextRun}", nextRunUtc);

				try
				{
					// Vent til kl. 03:30
					await Task.Delay(delay, stoppingToken);

					if (!IsExecutionSafe())
					{
						_logger.LogWarning("Kørsel blokeret: Sikkerhedsmekanisme aktiveret.");
						continue;
					}

					_logger.LogInformation("Starter daglig værdiberegning...");
					decimal runningTotal = 0;

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
					if (DateTime.UtcNow >= new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc))
					{
						eodResponse = await GetStockPrizeAsync(stoppingToken);
					}
					else
					{
						_logger.LogInformation("Bruger testdata for aktier (før marts 2026).");
						eodResponse = _testDataClass.Test();
					}

					if (eodResponse != null)
					{
						decimal stockValue = CalculateTotalStockValueAsync(eodResponse);
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

				// LIVE ENDPOINT
				string tokenEndpoint = "https://live.logonvalidation.net/token";
				var response = await client.PostAsync(tokenEndpoint, requestData, stoppingToken);

				if (!response.IsSuccessStatusCode)
				{
					_logger.LogError("Saxo LIVE afviste refresh token.");
					return null;
				}

				var json = await response.Content.ReadAsStringAsync(stoppingToken);
				using var doc = JsonDocument.Parse(json);

				string newAccessToken = doc.RootElement.GetProperty("access_token").GetString()!;
				string newRefreshToken = doc.RootElement.GetProperty("refresh_token").GetString()!;

				// Gem det nye roterede token krypteret
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

			// LIVE ENDPOINT (Uden /sim/)
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