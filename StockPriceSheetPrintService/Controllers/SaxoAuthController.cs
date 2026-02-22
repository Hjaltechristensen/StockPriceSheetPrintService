using Microsoft.AspNetCore.Mvc;
using StockPriceSheetPrintService.Models;
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

		// Brug konfiguration til disse, så du let kan skifte mellem Sim og Live
		private string AuthEndpoint => _configuration["Saxo:AuthEndpoint"] ?? "https://sim.logonvalidation.net/authorize";
		private string TokenEndpoint => _configuration["Saxo:TokenEndpoint"] ?? "https://sim.logonvalidation.net/token";
		private string ApiBaseUrl => _configuration["Saxo:ApiBaseUrl"] ?? "https://gateway.saxobank.com/sim/openapi";

		private string RedirectUrl => _configuration["Saxo:RedirectUrl"] ?? "http://192.168.1.239:5151/saxo/callback";
		private const string TokenPath = "/app/data/refresh_token.bin";

		[HttpGet("login")]
		public IActionResult GetLoginUrl()
		{
			var authUrl = $"{AuthEndpoint}?client_id={AppKey}&response_type=code&redirect_uri={Uri.EscapeDataString(RedirectUrl)}";
			_logger.LogInformation("Login URL genereret");
			return Ok(new { LoginUrl = authUrl });
		}

		[HttpGet("callback")]
		public async Task<IActionResult> Callback([FromQuery] string code)
		{
			if (string.IsNullOrEmpty(code)) return BadRequest("Ingen kode modtaget.");

			try
			{
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

				var tokenResponse = await client.PostAsync(TokenEndpoint, tokenRequest);
				var tokenData = await tokenResponse.Content.ReadAsStringAsync();

				if (!tokenResponse.IsSuccessStatusCode)
				{
					_logger.LogError("Saxo token fejl: {Data}", tokenData);
					return BadRequest("Kunne ikke veksle kode til token.");
				}

				var jsonDoc = JsonDocument.Parse(tokenData);
				var accessToken = jsonDoc.RootElement.GetProperty("access_token").GetString();
				var refreshToken = jsonDoc.RootElement.GetProperty("refresh_token").GetString();

				// --- SIKKER GEMNING (Krypteret) ---
				if (!string.IsNullOrEmpty(refreshToken))
				{
					string encryptedToken = StockPriceSheetPrintService.Krypto.TokenEncryptor.Encrypt(refreshToken, EncryptionKey);

					var dir = Path.GetDirectoryName(TokenPath);
					if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);

					await System.IO.File.WriteAllTextAsync(TokenPath, encryptedToken);
					_logger.LogInformation("Refresh token gemt sikkert på disken.");
				}

				// Hent balance som bekræftelse
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
				var balanceResponse = await client.GetAsync($"{ApiBaseUrl}/port/v1/balances/me");

				if (!balanceResponse.IsSuccessStatusCode) return BadRequest("Token OK, men kunne ikke hente balance.");

				var balanceData = await balanceResponse.Content.ReadAsStringAsync();
				var balance = JsonSerializer.Deserialize<SaxoBalanceResponse>(balanceData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

				return Ok(new { Message = "Alt er sat op! Din worker vil nu køre automatisk.", Værdi = balance?.TotalValue });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Fejl i Saxo Callback");
				return StatusCode(500, "Intern serverfejl.");
			}
		}
	}
}
