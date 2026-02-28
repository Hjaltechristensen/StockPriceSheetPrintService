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
			string redirectUrl = _configuration["Saxo:RedirectUrl"] ?? "http://127.0.0.1:5151/saxo/callback";
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
			_logger.LogInformation("\n\n");
			_logger.LogInformation("╔═══════════════════════════════════════════╗");
			_logger.LogInformation("║  STOCKPRIZE WORKER STARTET				║");
			_logger.LogInformation("╚═══════════════════════════════════════════╝");

			ManualLoginIfNeeded();

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					var localTime = GetLocalTime(DateTimeOffset.UtcNow);
					var nextRunUtc = GetNextRunTime(localTime, 03, 30);
					var delay = nextRunUtc - DateTimeOffset.UtcNow;
					if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;


					_logger.LogInformation("[SCHEDULER] Næste kørsel planlagt til: {nextRun} (om {hours:F1} timer)", nextRunUtc, delay.TotalHours);

					while (DateTimeOffset.UtcNow < nextRunUtc && !stoppingToken.IsCancellationRequested)
					{
						var timeUntilJob = nextRunUtc - DateTimeOffset.UtcNow;
						var refreshDelay = TimeSpan.FromMinutes(45);

						if (refreshDelay > timeUntilJob)
							break; // Tæt på job-tidspunkt, lad jobbet håndtere det
						_logger.LogInformation("[SCHEDULER] Session refresh kl. {minutes} for at holde token i live...", DateTime.Now.AddHours(1).AddMinutes(refreshDelay.TotalMinutes));
						await Task.Delay(refreshDelay, stoppingToken);
						_logger.LogInformation("\n{}[SCHEDULER] Udfører token refresh for at holde session i live...", DateTime.Now);
						await GetSaxoAccessTokenAsync(stoppingToken);
					}

					// Vent de sidste minutter til præcis 03:30
					var finalDelay = nextRunUtc - DateTimeOffset.UtcNow;
					if (finalDelay > TimeSpan.Zero)
						await Task.Delay(finalDelay, stoppingToken);

					await RunJobAsync(stoppingToken);

				}
				catch (OperationCanceledException)
				{
					_logger.LogInformation("[SCHEDULER] ✓ Worker stopper normalt...");
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "[SCHEDULER] ✗ UVENTET FEJL i scheduleren!");
				}
			}
		}

		public async Task RunJobAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation("╔═══════════════════════════════════════════╗");
			_logger.LogInformation("║  JOB KØRSEL STARTER - {time:HH:mm:ss}              ║", DateTime.Now);
			_logger.LogInformation("╚═══════════════════════════════════════════╝");

			if (!IsExecutionSafe())
			{
				_logger.LogWarning("[JOB] ✗ KØRSEL BLOKERET: Sikkerhedsmekanisme aktiveret (for mange kørsler)");
				return;
			}

			try
			{
				decimal nordnetCash = 1116m;
				string runningTotal = "=" + nordnetCash;

				// --- 1. SAXO BALANCE ---
				_logger.LogInformation("[JOB] [1/4] Starter Saxo balance hentning...");
				string? saxoToken = await GetSaxoAccessTokenAsync(stoppingToken);
				if (saxoToken != null)
				{
					decimal saxoBalance = await GetSaxoBalanceAsync(saxoToken, stoppingToken);
					runningTotal += "+" + saxoBalance;
					_logger.LogInformation("[JOB] ✓ Saxo balance: {val:F2} DKK", saxoBalance);
				}
				else
				{
					_logger.LogWarning("[JOB] ⚠ Saxo balance ikke tilgængelig (log ind første gang via /saxo/login)");
				}


					// --- 2. AKTIER (MarketStack) ---
					_logger.LogInformation("[JOB] [2/4] Starter hentning af Nordnet priser...");
					EodResponse? eodResponse;
					DateTime liveStockDate = DateTime.Parse(_configuration["StockApi:LiveFromDate"] ?? "2026-03-01");
					if (DateTime.UtcNow >= liveStockDate)
					{
						eodResponse = await GetStockPricesAsync(stoppingToken);
					}
					else
					{
						_logger.LogInformation("[JOB] Bruger testdata for aktier (før {date})", liveStockDate.ToString("dd/MM/yyyy"));
						eodResponse = _testDataClass.Test();
					}

					if (eodResponse != null)
					{
						decimal stockValue = CalculateTotalStockValue(eodResponse);
						runningTotal += "+" + stockValue;
						_logger.LogInformation("[JOB] ✓ Aktieværdi: {val:F2} DKK", stockValue);
					}
					else
					{
						_logger.LogWarning("[JOB] ⚠ Kunne ikke hente aktiepriser");
					}

					// --- 3. FONDE (Scraper) ---
					_logger.LogInformation("[JOB] [3/4] Starter hentning af fondsværdi...");
					decimal fundValue = await FindTotalFundValue(stoppingToken);
					runningTotal += "+" + fundValue;
					_logger.LogInformation("[JOB] ✓ Fondsværdi: {val:F2} DKK", fundValue);

					// --- 4. OPDATER GOOGLE SHEETS ---
					_logger.LogInformation("[JOB] [4/4] TOTAL værdi: {total:F2} DKK", runningTotal);
					var sheetsKey = _configuration["SheetsApi:SheetsKey"];
					if (!string.IsNullOrEmpty(sheetsKey))
					{
						_logger.LogInformation("[JOB] Sender data til Google Sheets...");
						await _updateCellAsync.UpdateGoogleSheetsCellAsync(sheetsKey, "Ark1", runningTotal);
						LogExecution();
						_logger.LogInformation("[JOB] ✓ Google Sheets opdateret succesfuldt");
					}
					else
					{
						_logger.LogError("[JOB] ✗ FEJL: SheetsKey mangler! Kunne ikke gemme resultatet.");
					}

					_logger.LogInformation("╔═══════════════════════════════════════════╗");
					_logger.LogInformation("║  JOB AFSLUTTET - SUCCESFULDT               ║");
					_logger.LogInformation("║  Total værdi: {total:F2} DKK", runningTotal);
					_logger.LogInformation("╚═══════════════════════════════════════════╝");
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "[JOB] ✗ UVENTET FEJL under job kørsel!");
					_logger.LogError("[JOB] Exception Type: {type}", ex.GetType().Name);
					_logger.LogError("[JOB] Stack Trace: {stackTrace}", ex.StackTrace);
					throw;
				}
			}

		internal async Task<string?> GetSaxoAccessTokenAsync(CancellationToken stoppingToken)
		{
			string tokenPath = "/app/data/refresh_token.bin";
			string? encryptionKey = _configuration["Saxo:EncryptionKey"];

			if (!File.Exists(tokenPath))
			{
				_logger.LogWarning("[SAXO-TOKEN] ✗ ADVARSEL: Ingen refresh token fundet på: {path}", tokenPath);
				_logger.LogWarning("[SAXO-TOKEN] Du skal logge ind manuelt først via: http://127.0.0.1:5151/saxo/callback");
				return null;
			}

			try
			{
				string encryptedToken = await File.ReadAllTextAsync(tokenPath, stoppingToken);

				if (string.IsNullOrEmpty(encryptionKey))
				{
					_logger.LogError("[SAXO-TOKEN] ✗ FEJL: Saxo:EncryptionKey ikke konfigureret!");
					return null;
				}

				string refreshToken = TokenEncryptor.Decrypt(encryptedToken, encryptionKey);

				var client = _httpClientFactory.CreateClient();
				var requestData = new FormUrlEncodedContent(new Dictionary<string, string>
				{
					{ "grant_type", "refresh_token" },
					{ "refresh_token", refreshToken },
					{ "client_id", _configuration["Saxo:AppKey"] },
					{ "client_secret", _configuration["Saxo:AppSecret"] }
				});

				string tokenEndpoint = _configuration["Saxo:TokenEndpoint"] ?? "https://live.logonvalidation.net/token";

				var response = await client.PostAsync(tokenEndpoint, requestData, stoppingToken);
				var responseBody = await response.Content.ReadAsStringAsync(stoppingToken); // Læs kun én gang

				if (!response.IsSuccessStatusCode)
				{
					_logger.LogError("[SAXO-TOKEN] ✗ FEJL: Saxo afviste refresh token!");
					_logger.LogError("[SAXO-TOKEN] Status: {status}", (int)response.StatusCode);
					_logger.LogError("[SAXO-TOKEN] Response: {response}", responseBody);
					return null;
				}

				using var doc = JsonDocument.Parse(responseBody); // Brug den allerede læste body

				string newAccessToken = doc.RootElement.GetProperty("access_token").GetString()!;
				string newRefreshToken = doc.RootElement.GetProperty("refresh_token").GetString()!;

				string encryptedNewToken = TokenEncryptor.Encrypt(newRefreshToken, encryptionKey);
				await File.WriteAllTextAsync(tokenPath, encryptedNewToken, stoppingToken);

				_logger.LogInformation("[SAXO-TOKEN] ✓ Token refresh fuldført");

				return newAccessToken;
			}
			catch (FileNotFoundException ex)
			{
				_logger.LogError(ex, "[SAXO-TOKEN] ✗ FEJL: Token fil ikke fundet!");
				return null;
			}
			catch (UnauthorizedAccessException ex)
			{
				_logger.LogError(ex, "[SAXO-TOKEN] ✗ FEJL: Ingen adgang til token fil (permissions problem!)");
				return null;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[SAXO-TOKEN] ✗ UVENTET FEJL under Saxo token refresh!");
				_logger.LogError("[SAXO-TOKEN] Exception Type: {exceptionType}", ex.GetType().Name);
				_logger.LogError("[SAXO-TOKEN] Stack Trace: {stackTrace}", ex.StackTrace);
				return null;
			}
		}

		private async Task<decimal> GetSaxoBalanceAsync(string accessToken, CancellationToken stoppingToken)
		{
			try
			{
				var client = _httpClientFactory.CreateClient();
				client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

				string apiEndpoint = "https://gateway.saxobank.com/openapi/port/v1/balances/me";

				var response = await client.GetAsync(apiEndpoint, stoppingToken);

				if (response.IsSuccessStatusCode)
				{
					var json = await response.Content.ReadAsStringAsync(stoppingToken);

					using var doc = JsonDocument.Parse(json);
					var totalValue = doc.RootElement.GetProperty("TotalValue").GetDecimal();

					return totalValue;
				}

				var errorContent = await response.Content.ReadAsStringAsync(stoppingToken);
				_logger.LogError("[SAXO-BALANCE] ✗ FEJL: Saxo returnerede fejl!");
				_logger.LogError("[SAXO-BALANCE] Status: {status}", (int)response.StatusCode);
				_logger.LogError("[SAXO-BALANCE] Response: {response}", errorContent);
				return 0;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[SAXO-BALANCE] ✗ UVENTET FEJL ved hentning af balance!");
				_logger.LogError("[SAXO-BALANCE] Exception Type: {type}", ex.GetType().Name);
				return 0;
			}
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
