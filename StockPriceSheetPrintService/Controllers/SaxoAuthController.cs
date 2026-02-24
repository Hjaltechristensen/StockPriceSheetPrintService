using Microsoft.AspNetCore.Mvc;
using StockPriceSheetPrintService.Models;
using StockPrizeSenderService;
using StockPrizeSenderService.GoogleSheets;
using System.Net.Http.Headers;
using System.Text.Json;

namespace StockPriceSheetPrintService.Controllers
{
	[ApiController]
	[Route("saxo")]
	public class SaxoAuthController : ControllerBase
	{
		private readonly IConfiguration _configuration;
		private readonly ILogger<SaxoAuthController> _logger;
		private readonly IHttpClientFactory _httpClientFactory;

		public SaxoAuthController(IConfiguration configuration, ILogger<SaxoAuthController> logger, IHttpClientFactory httpClientFactory)
		{
			_configuration = configuration;
			_logger = logger;
			_httpClientFactory = httpClientFactory;
		}

		private string AppKey => _configuration["Saxo:AppKey"] ?? throw new InvalidOperationException("Saxo:AppKey missing");
		private string AppSecret => _configuration["Saxo:AppSecret"] ?? throw new InvalidOperationException("Saxo:AppSecret missing");
		private string EncryptionKey => _configuration["Saxo:EncryptionKey"] ?? throw new InvalidOperationException("Saxo:EncryptionKey missing");
		private string AuthEndpoint => _configuration["Saxo:AuthEndpoint"] ?? throw new InvalidOperationException("AuthEndpoint missing");
		private string TokenEndpoint => _configuration["Saxo:TokenEndpoint"] ?? throw new InvalidOperationException("TokenEndpoint missing");
		private string ApiBaseUrl => _configuration["Saxo:ApiBaseUrl"] ?? throw new InvalidOperationException("ApiBaseUrl missing");

		private string RedirectUrl => _configuration["Saxo:RedirectUrl"] ?? throw new InvalidOperationException("RedirectUrl missing");
		private const string TokenPath = "/app/data/refresh_token.bin";

		[HttpGet("login")]
		public IActionResult GetLoginUrl()
		{
			try
			{
				// LIVE adresser!
				string clientId = AppKey;
				string redirectUri = RedirectUrl;

				_logger.LogInformation("[SAXO-LOGIN] Genererer login URL");
				_logger.LogInformation("[SAXO-LOGIN] Auth Endpoint: {endpoint}", AuthEndpoint);
				_logger.LogInformation("[SAXO-LOGIN] Client ID: {clientId}", clientId[..Math.Min(4, clientId.Length)] + "****");
				_logger.LogInformation("[SAXO-LOGIN] Redirect URI: {redirectUri}", redirectUri);

				// Uri.EscapeDataString er stadig livsvigtig
				var authUrl = $"{AuthEndpoint}?client_id={clientId}&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}";

				_logger.LogInformation("[SAXO-LOGIN] ✓ Login URL genereret succesfuldt");

				Console.WriteLine("\n************************************************************");
				Console.WriteLine("KOPIÉR DETTE LINK TIL DIN BROWSER FOR AT GIVE ADGANG:");
				Console.WriteLine(authUrl);
				Console.WriteLine("************************************************************\n");

				return Content(authUrl);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[SAXO-LOGIN] ✗ Fejl ved generering af login URL");
				return StatusCode(500, $"Fejl: {ex.Message}");
			}
		}

		[HttpGet("callback")]
		public async Task<IActionResult> Callback([FromQuery] string code)
		{
			_logger.LogInformation("[SAXO-CALLBACK] ========== OAUTH CALLBACK STARTER ==========");

			if (string.IsNullOrEmpty(code))
			{
				_logger.LogError("[SAXO-CALLBACK] ✗ FEJL: Ingen 'code' parameter modtaget fra Saxo!");
				_logger.LogError("[SAXO-CALLBACK] Query string var: {queryString}", Request.QueryString);
				return BadRequest("❌ Ingen kode modtaget fra Saxo. Tjek at redirect URL er korrekt.");
			}

			_logger.LogInformation("[SAXO-CALLBACK] ✓ Auth code modtaget (længde: {length} tegn)", code.Length);

			try
			{
				_logger.LogInformation("[SAXO-CALLBACK] [STEP 1] Starter token exchange");
				_logger.LogInformation("[SAXO-CALLBACK] Token Endpoint: {endpoint}", TokenEndpoint);
				_logger.LogInformation("[SAXO-CALLBACK] Redirect URL: {redirectUrl}", RedirectUrl);

				// Brug factory i stedet for 'new'
				var client = _httpClientFactory.CreateClient();

				var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
				{
					{ "grant_type", "authorization_code" },
					{ "code", code },
					{ "redirect_uri", RedirectUrl },
					{ "client_id", AppKey },
					{ "client_secret", AppSecret }
				});

				_logger.LogInformation("[SAXO-CALLBACK] [STEP 1.1] POST request sendes til token endpoint...");
				var tokenResponse = await client.PostAsync(TokenEndpoint, tokenRequest);
				var tokenData = await tokenResponse.Content.ReadAsStringAsync();

				_logger.LogInformation("[SAXO-CALLBACK] [STEP 1.2] Svar status: {statusCode}", (int)tokenResponse.StatusCode);

				if (!tokenResponse.IsSuccessStatusCode)
				{
					_logger.LogError("[SAXO-CALLBACK] ✗ FEJL: Token request afvist af Saxo!");
					_logger.LogError("[SAXO-CALLBACK] Status Code: {statusCode}", (int)tokenResponse.StatusCode);
					_logger.LogError("[SAXO-CALLBACK] Response Body: {responseBody}", tokenData);
					return BadRequest($"❌ Saxo afviste token request. Status: {tokenResponse.StatusCode}. Se server logs for detaljer.");
				}

				_logger.LogInformation("[SAXO-CALLBACK] ✓ Token response succesfuldt modtaget");

				_logger.LogInformation("[SAXO-CALLBACK] [STEP 2] Parser tokens fra JSON response");
				JsonDocument jsonDoc;
				try
				{
					jsonDoc = JsonDocument.Parse(tokenData);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "[SAXO-CALLBACK] ✗ FEJL: Kunne ikke parse JSON response fra Saxo");
					_logger.LogError("[SAXO-CALLBACK] Raw response: {response}", tokenData);
					return BadRequest("❌ Uventet format fra Saxo API");
				}

				var accessToken = jsonDoc.RootElement.GetProperty("access_token").GetString();
				var refreshToken = jsonDoc.RootElement.GetProperty("refresh_token").GetString();

				_logger.LogInformation("[SAXO-CALLBACK] ✓ Access Token modtaget (længde: {length})", accessToken?.Length ?? 0);
				_logger.LogInformation("[SAXO-CALLBACK] ✓ Refresh Token modtaget (længde: {length})", refreshToken?.Length ?? 0);

				// --- SIKKER GEMNING (Krypteret) ---
				_logger.LogInformation("[SAXO-CALLBACK] [STEP 3] Starter krypton og gemning af refresh token");
				if (!string.IsNullOrEmpty(refreshToken))
				{
					try
					{
						_logger.LogInformation("[SAXO-CALLBACK] [STEP 3.1] Krypterer refresh token med EncryptionKey...");
						string encryptedToken = StockPriceSheetPrintService.Krypto.TokenEncryptor.Encrypt(refreshToken, EncryptionKey);
						_logger.LogInformation("[SAXO-CALLBACK] ✓ Token krypteret succesfuldt (længde: {length})", encryptedToken.Length);

						var dir = Path.GetDirectoryName(TokenPath);
						_logger.LogInformation("[SAXO-CALLBACK] [STEP 3.2] Token path: {path}", TokenPath);
						_logger.LogInformation("[SAXO-CALLBACK] [STEP 3.3] Directory: {dir}", dir);

						if (!Directory.Exists(dir))
						{
							_logger.LogInformation("[SAXO-CALLBACK] Directory eksisterer ikke. Opretter: {dir}", dir);
							Directory.CreateDirectory(dir!);
						}

						_logger.LogInformation("[SAXO-CALLBACK] [STEP 3.4] Skriver til fil...");
						await System.IO.File.WriteAllTextAsync(TokenPath, encryptedToken);
						_logger.LogInformation("[SAXO-CALLBACK] ✓ Refresh token gemt sikkert på: {path}", TokenPath);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "[SAXO-CALLBACK] ✗ FEJL: Kunne ikke gemme refresh token på disk!");
						return BadRequest("❌ Kunne ikke gemme refresh token. Se server logs for detaljer.");
					}
				}

				// Hent balance som bekræftelse
				_logger.LogInformation("[SAXO-CALLBACK] [STEP 4] Starter balance hentning som bekræftelse");
				_logger.LogInformation("[SAXO-CALLBACK] API Base URL: {baseUrl}", ApiBaseUrl);

				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

				_logger.LogInformation("[SAXO-CALLBACK] [STEP 4.1] GET request til balance endpoint...");
				var balanceResponse = await client.GetAsync($"{ApiBaseUrl}/port/v1/balances/me");
				var balanceData = await balanceResponse.Content.ReadAsStringAsync();

				_logger.LogInformation("[SAXO-CALLBACK] [STEP 4.2] Balance response status: {statusCode}", (int)balanceResponse.StatusCode);

				if (!balanceResponse.IsSuccessStatusCode)
				{
					_logger.LogError("[SAXO-CALLBACK] ✗ FEJL: Kunne ikke hente balance");
					_logger.LogError("[SAXO-CALLBACK] Response: {response}", balanceData);
					return BadRequest("❌ Token OK, men kunne ikke hente balance.");
				}

				_logger.LogInformation("[SAXO-CALLBACK] ✓ Balance hentet succesfuldt");

				SaxoBalanceResponse? balance = null;
				try
				{
					balance = JsonSerializer.Deserialize<SaxoBalanceResponse>(balanceData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
					_logger.LogInformation("[SAXO-CALLBACK] ✓ Balance deserializeret: {totalValue} {currency}", balance?.TotalValue, balance?.Currency);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "[SAXO-CALLBACK] ✗ FEJL: Kunne ikke parse balance response");
					_logger.LogError("[SAXO-CALLBACK] Raw response: {response}", balanceData);
				}

				_logger.LogInformation("[SAXO-CALLBACK] ========== ✓ CALLBACK SUCCESFULDT ==========");

				var responseObj = new
				{
					Message = "✓ Alt er sat op! Din worker vil nu køre automatisk.",
					Værdi = balance?.TotalValue,
					Valuta = balance?.Currency,
					NextRunTime = "Check logs for næste planlagte kørsel"
				};

				_logger.LogInformation("[SAXO-CALLBACK] Returnerer success response: {@response}", responseObj);

				return Ok(responseObj);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[SAXO-CALLBACK] ✗ UVENTET FEJL I CALLBACK!");
				_logger.LogError("[SAXO-CALLBACK] Exception Type: {exceptionType}", ex.GetType().Name);
				_logger.LogError("[SAXO-CALLBACK] Stack Trace: {stackTrace}", ex.StackTrace);
				return StatusCode(500, $"❌ Intern serverfejl: {ex.Message}");
			}
		}

		[HttpPost("trigger")]
		public async Task<IActionResult> TriggerJob([FromServices] StockprizeWorker worker, CancellationToken ct)
		{
			_logger.LogInformation("Manuel trigger aktiveret via HTTP.");
			await worker.RunJobAsync(ct);
			return Ok(new { Message = "Kørsel gennemført." });
		}

		[HttpPost("sheets")]
		public async Task<IActionResult> TriggerSheets([FromServices] UpdateCellAsync worker)
		{
			_logger.LogInformation("Manuel sheets trigger aktiveret via HTTP");
			var sheetsKey = _configuration["SheetsApi:SheetsKey"];
			if (sheetsKey != null)
			{
				await worker.UpdateGoogleSheetsCellAsync(sheetsKey, "Ark1", 20202m);
				return Ok(new { Message = "Sheets kørsel gennemført"});
			}
			return Ok(new { Message = "Sheets failed" });
		}
	}
}
