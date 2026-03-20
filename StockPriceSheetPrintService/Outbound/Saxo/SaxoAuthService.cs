using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports;
using System.Net.Http.Headers;
using System.Text.Json;

namespace StockPriceSheetPrintService.Outbound.Saxo
{
	public class SaxoAuthService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<SaxoAuthService> logger) : ISaxoAuthService
	{
		private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
		private readonly ILogger<SaxoAuthService> _logger = logger;
		private readonly string _tokenEndpoint = configuration["Saxo:TokenEndpoint"] ?? throw new InvalidOperationException("TokenEndpoint missing");
		private readonly string _redirectUrl = configuration["Saxo:RedirectUrl"] ?? throw new InvalidOperationException("RedirectUrl missing");
		private readonly string _appKey = configuration["Saxo:AppKey"] ?? throw new InvalidOperationException("Saxo:AppKey missing");
		private readonly string _appSecret = configuration["Saxo:AppSecret"] ?? throw new InvalidOperationException("Saxo:AppSecret missing");
		private readonly string _apiBaseUrl = configuration["Saxo:ApiBaseUrl"] ?? throw new InvalidOperationException("ApiBaseUrl missing");
		private readonly string _authEndpoint = configuration["Saxo:AuthEndpoint"] ?? throw new InvalidOperationException("AuthEndpoint missing");

		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true
		};

		public string BuildLoginUrl()
		{
			return $"{_authEndpoint}?client_id={_appKey}&response_type=code&redirect_uri={Uri.EscapeDataString(_redirectUrl)}";
		}

		public async Task<SaxoTokenResult> ExchangeCodeForTokensAsync(string code, CancellationToken ct)
		{
			var client = _httpClientFactory.CreateClient();

			var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
			{
				{ "grant_type", "authorization_code" },
				{ "code", code },
				{ "redirect_uri", _redirectUrl },
				{ "client_id", _appKey },
				{ "client_secret", _appSecret }
			});

			var tokenResponse = await client.PostAsync(_tokenEndpoint, tokenRequest, ct);
			var tokenData = await tokenResponse.Content.ReadAsStringAsync(ct);

			if (!tokenResponse.IsSuccessStatusCode)
			{
				_logger.LogError("[SAXO-AUTH] Token request afvist. Status: {status}, Body: {body}",
					(int)tokenResponse.StatusCode, tokenData);
				throw new HttpRequestException($"Saxo afviste token request. Status: {tokenResponse.StatusCode}");
			}

			var jsonDoc = JsonDocument.Parse(tokenData);
			return new SaxoTokenResult
			{
				AccessToken = jsonDoc.RootElement.GetProperty("access_token").GetString() ?? string.Empty,
				RefreshToken = jsonDoc.RootElement.GetProperty("refresh_token").GetString() ?? string.Empty
			};
		}

		public async Task<SaxoBalanceResponse> GetBalanceAsync(string accessToken, CancellationToken ct)
		{
			var client = _httpClientFactory.CreateClient();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

			var balanceResponse = await client.GetAsync($"{_apiBaseUrl}/port/v1/balances/me", ct);
			var balanceData = await balanceResponse.Content.ReadAsStringAsync(ct);

			if (!balanceResponse.IsSuccessStatusCode)
			{
				_logger.LogError("[SAXO-AUTH] Kunne ikke hente balance. Status: {status}, Body: {body}",
					(int)balanceResponse.StatusCode, balanceData);
				throw new HttpRequestException("Kunne ikke hente balance fra Saxo.");
			}

			return JsonSerializer.Deserialize<SaxoBalanceResponse>(balanceData, JsonOptions)
			?? throw new InvalidOperationException("Tom balance response fra Saxo.");
		}
	}
}
