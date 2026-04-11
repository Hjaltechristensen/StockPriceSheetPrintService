using StockPriceSheetPrintService.Service.Ports;
using System.Text.Json;

namespace StockPriceSheetPrintService.Outbound.Saxo
{
	public class SaxoTokenService(ILogger<SaxoTokenService> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory, ITokenStore tokenStore, ISaxoService saxoService) : ISaxoTokenService
	{
		private readonly ILogger<SaxoTokenService> _logger = logger;
		private readonly ITokenStore _tokenStore = tokenStore;
		private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
		private readonly ISaxoService _service = saxoService;
		private readonly string _appKey = configuration["Saxo:AppKey"] ?? throw new InvalidOperationException("Saxo:AppKey missing");
		private readonly string _appSecret = configuration["Saxo:AppSecret"] ?? throw new InvalidOperationException("Saxo:AppSecret missing");
		private readonly string _tokenEndpoint = configuration["Saxo:TokenEndpoint"] ?? throw new InvalidOperationException("Saxo:TokenEndpoint missing");

		public async Task<string?> GetAccessTokenAsync(CancellationToken ct)
		{
			if (!_tokenStore.TokenExists())
			{
				_logger.LogWarning("[SAXO-TOKEN] Ingen refresh token fundet – log ind via /saxo/login");
				await _service.BuildLoginUrl();
				return null;
			}

			try
			{
				var refreshToken = await _tokenStore.ReadRefreshTokenAsync(ct);
				if (refreshToken == null) return null;

				var client = _httpClientFactory.CreateClient();
				var requestData = new FormUrlEncodedContent(new Dictionary<string, string>
				{
					{ "grant_type", "refresh_token" },
					{ "refresh_token", refreshToken },
					{ "client_id", _appKey },
					{ "client_secret", _appSecret }
				});

				var response = await client.PostAsync(_tokenEndpoint, requestData, ct);
				var responseBody = await response.Content.ReadAsStringAsync(ct);

				if (!response.IsSuccessStatusCode)
				{
					_logger.LogError("[SAXO-TOKEN] Saxo afviste refresh token. Status: {status}, Body: {body}",
						(int)response.StatusCode, responseBody);
					await _service.BuildLoginUrl();
					return null;
				}

				using var doc = JsonDocument.Parse(responseBody);
				var newAccessToken = doc.RootElement.GetProperty("access_token").GetString()!;
				var newRefreshToken = doc.RootElement.GetProperty("refresh_token").GetString()!;

				await _tokenStore.SaveRefreshTokenAsync(newRefreshToken, ct);
				_logger.LogInformation("[SAXO-TOKEN] Token refresh fuldført");

				return newAccessToken;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[SAXO-TOKEN] Uventet fejl under token refresh");
				return null;
			}
		}
	}
}
