using StockPriceSheetPrintService.Service.Ports.Outbound;
using System.Text.Json;

namespace StockPriceSheetPrintService.Outbound.Saxo
{
	public class SaxoTokenService(ILogger<SaxoTokenService> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory, ITokenStore tokenStore, ISaxoAuthService saxoAuthService, IDiscordNotifier discordNotifier) : ISaxoTokenService
	{
		private readonly ILogger<SaxoTokenService> _logger = logger;
		private readonly ITokenStore _tokenStore = tokenStore;
		private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
		private readonly ISaxoAuthService _saxoAuthService = saxoAuthService;
		private readonly IDiscordNotifier _discordNotifier = discordNotifier;
		private readonly string _appKey = configuration["Saxo:AppKey"] ?? throw new InvalidOperationException("Saxo:AppKey missing");
		private readonly string _appSecret = configuration["Saxo:AppSecret"] ?? throw new InvalidOperationException("Saxo:AppSecret missing");
		private readonly string _tokenEndpoint = configuration["Saxo:TokenEndpoint"] ?? throw new InvalidOperationException("Saxo:TokenEndpoint missing");

		private async Task NotifyLoginRequired(CancellationToken ct)
		{
			var loginUrl = await _saxoAuthService.BuildLoginUrl();
			await _discordNotifier.SendLoginUrlAsync(loginUrl, ct);
		}

		public async Task<string?> GetAccessTokenAsync(ClientContext ctx, CancellationToken ct)
		{
			var refreshToken = await _tokenStore.ReadRefreshTokenAsync(ct);
			if (refreshToken == null)
			{
				_logger.LogWarning("[SAXO-TOKEN] No refresh token found – log in via /saxo/login");
				await NotifyLoginRequired(ct);
				return null;
			}

				try
				{
					var client = _httpClientFactory.CreateClient();
					using var requestData = new FormUrlEncodedContent(new Dictionary<string, string>
					{
						{ "grant_type", "refresh_token" },
						{ "refresh_token", refreshToken },
						{ "client_id", _appKey },
						{ "client_secret", _appSecret }
					});

					var response = await client.PostAsync(_tokenEndpoint, requestData, ct);

					if (!response.IsSuccessStatusCode)
					{
						_logger.LogError("[SAXO-TOKEN] Saxo rejected refresh token. Status: {status}",
							(int)response.StatusCode);
						await NotifyLoginRequired(ct);
						return null;
					}

					var responseBody = await response.Content.ReadAsStringAsync(ct);

					try
					{
						using var doc = JsonDocument.Parse(responseBody);
						if (!doc.RootElement.TryGetProperty("access_token", out var accessTokenElement) ||
							!doc.RootElement.TryGetProperty("refresh_token", out var refreshTokenElement))
						{
							_logger.LogError("[SAXO-TOKEN] Token response missing expected properties");
							await NotifyLoginRequired(ct);
							return null;
						}

						var newAccessToken = accessTokenElement.GetString();
						var newRefreshToken = refreshTokenElement.GetString();

						if (string.IsNullOrEmpty(newAccessToken) || string.IsNullOrEmpty(newRefreshToken))
						{
							_logger.LogError("[SAXO-TOKEN] Token response contains empty values");
							await NotifyLoginRequired(ct);
							return null;
						}

						await _tokenStore.SaveRefreshTokenAsync(newRefreshToken, ct);
						_logger.LogInformation("[SAXO-TOKEN] Token refresh completed");

						return newAccessToken;
					}
					catch (JsonException ex)
					{
						_logger.LogError(ex, "[SAXO-TOKEN] Error parsing token response");
						await NotifyLoginRequired(ct);
						return null;
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "[SAXO-TOKEN] Unexpected error during token refresh");
					await NotifyLoginRequired(ct);
					return null;
				}
			}
	}
}
