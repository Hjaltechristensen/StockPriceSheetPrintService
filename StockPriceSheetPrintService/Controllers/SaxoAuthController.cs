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

		public SaxoAuthController(IConfiguration configuration, ILogger<SaxoAuthController> logger)
		{
			_configuration = configuration;
			_logger = logger;
		}

		private string AppKey => _configuration["Saxo:AppKey"] ?? throw new InvalidOperationException("Saxo:AppKey not configured");
		private string AppSecret => _configuration["Saxo:AppSecret"] ?? throw new InvalidOperationException("Saxo:AppSecret not configured");
		private const string RedirectUrl = "http://192.168.1.239:5151/saxo/callback";
		private const string AuthEndpoint = "https://live.logonvalidation.net/authorize";
		private const string TokenEndpoint = "https://live.logonvalidation.net/token";
		private string ApiBaseUrl => _configuration["Saxo:ApiBaseUrl"] ?? "https://gateway.saxobank.com/sim/openapi";

		[HttpGet("login")]
		public IActionResult GetLoginUrl()
		{
			// Vi bruger det specifikke AuthEndpoint her
			var authUrl = $"{AuthEndpoint}?client_id={AppKey}&response_type=code&redirect_uri={Uri.EscapeDataString(RedirectUrl)}";
			return Ok(new { LoginUrl = authUrl });
		}

		[HttpGet("callback")]
		public async Task<IActionResult> Callback([FromQuery] string code)
		{
			if (string.IsNullOrEmpty(code)) 
			{
				_logger.LogWarning("Callback kaldet uden kode");
				return BadRequest("Ingen kode modtaget fra Saxo.");
			}

			try
			{
				using var client = new HttpClient();

				// 1. Veksel kode til Token
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
					_logger.LogError("Fejl ved token-ombytning fra Saxo");
					return BadRequest("Fejl ved token-ombytning. Kontakt administrator.");
				}

				var jsonDoc = JsonDocument.Parse(tokenData);
				var accessToken = jsonDoc.RootElement.GetProperty("access_token").GetString();
				var refreshToken = jsonDoc.RootElement.GetProperty("refresh_token").GetString();

				_logger.LogInformation("Token modtaget fra Saxo - balance hentes");

				// 2. Hent Balance
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
				var balanceResponse = await client.GetAsync($"{ApiBaseUrl}/port/v1/balances/me");
				var balanceData = await balanceResponse.Content.ReadAsStringAsync();

				if (!balanceResponse.IsSuccessStatusCode)
				{
					_logger.LogError("Fejl ved hentning af balance fra Saxo");
					return BadRequest("Fejl ved hentning af balance. Kontakt administrator.");
				}

				// 3. Map til C# objekt (Model)
				var balance = JsonSerializer.Deserialize<SaxoBalanceResponse>(balanceData, new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});

				return Ok(new
				{
					Message = "Balance hentet succesfuldt",
					TotalVærdi = balance.TotalValue,
					Valuta = balance.Currency,
					Egenkapital = balance.CalculationAssetValue
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Uventet fejl i callback");
				return StatusCode(500, "Intern fejl. Kontakt administrator.");
			}
		}

		public async Task<string> RefreshAccessToken()
		{
			// Hent det gemte refresh token
			string savedRefreshToken = await System.IO.File.ReadAllTextAsync("refresh_token.txt");

				using var client = new HttpClient();
				var refreshRequest = new FormUrlEncodedContent(new Dictionary<string, string>
				{
					{ "grant_type", "refresh_token" },
					{ "refresh_token", savedRefreshToken },
					{ "client_id", AppKey },
					{ "client_secret", AppSecret }
				});

				var response = await client.PostAsync(TokenEndpoint, refreshRequest);
				var responseData = await response.Content.ReadAsStringAsync();

				if (!response.IsSuccessStatusCode)
				{
					_logger.LogError("Kunne ikke forny token");
					throw new Exception("Kunne ikke forny token");
				}

				var jsonDoc = JsonDocument.Parse(responseData);

				// VIGTIGT: Saxo sender ofte et NYT refresh_token med retur. 
				// Det skal du gemme sikkert oven i det gamle!
				var newRefreshToken = jsonDoc.RootElement.GetProperty("refresh_token").GetString();
			await System.IO.File.WriteAllTextAsync("refresh_token.txt", newRefreshToken);

			return jsonDoc.RootElement.GetProperty("access_token").GetString();
		}
	}
}
