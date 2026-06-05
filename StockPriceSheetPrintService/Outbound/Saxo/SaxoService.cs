using StockPriceSheetPrintService.OutboundDto.Saxo;
using StockPriceSheetPrintService.OutboundDto.Saxo.InstrumentDetails;
using StockPriceSheetPrintService.OutboundDto.Saxo.Positions;
using StockPriceSheetPrintService.OutboundDto.Saxo.Transactions;
using StockPriceSheetPrintService.Service.Ports.Outbound;
using System.Net.Http.Headers;
using System.Text.Json;

namespace StockPriceSheetPrintService.Outbound.Saxo
{
	public class SaxoService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ISaxoNetPositionStore saxoNetPositionStore, ILogger<SaxoService> logger) : ISaxoAuthService, ISaxoAccountService
	{
		private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
		private readonly ILogger<SaxoService> _logger = logger;
		private readonly string _tokenEndpoint = configuration["Saxo:TokenEndpoint"] ?? throw new InvalidOperationException("TokenEndpoint missing");
		private readonly string _redirectUrl = configuration["Saxo:RedirectUrl"] ?? throw new InvalidOperationException("RedirectUrl missing");
		private readonly string _appKey = configuration["Saxo:AppKey"] ?? throw new InvalidOperationException("Saxo:AppKey missing");
		private readonly string _appSecret = configuration["Saxo:AppSecret"] ?? throw new InvalidOperationException("Saxo:AppSecret missing");
		private readonly string _apiBaseUrl = configuration["Saxo:ApiBaseUrl"] ?? throw new InvalidOperationException("ApiBaseUrl missing");
		private readonly string _authEndpoint = configuration["Saxo:AuthEndpoint"] ?? throw new InvalidOperationException("AuthEndpoint missing");
		private readonly ISaxoNetPositionStore _saxoNetPositionStore = saxoNetPositionStore;
		private string? _clientKey;

		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true
		};

		public Task<string> BuildLoginUrl()
		{
			var loginUrl = $"{_authEndpoint}?client_id={_appKey}&response_type=code&redirect_uri={Uri.EscapeDataString(_redirectUrl)}";
			return Task.FromResult(loginUrl);
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
				_logger.LogError("[SAXO-AUTH] Token request rejected. Status: {status}, Body: {body}",
					(int)tokenResponse.StatusCode, tokenData);
				throw new HttpRequestException($"Saxo rejected token request. Status: {tokenResponse.StatusCode}");
			}

			var jsonDoc = JsonDocument.Parse(tokenData);
			return new SaxoTokenResult
			{
				AccessToken = jsonDoc.RootElement.GetProperty("access_token").GetString() ?? string.Empty,
				RefreshToken = jsonDoc.RootElement.GetProperty("refresh_token").GetString() ?? string.Empty
			};
		}

		public async Task<SaxoBalanceResponse?> GetBalanceAsync(string accessToken, CancellationToken ct)
		{
			var client = _httpClientFactory.CreateClient();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

			var balanceResponse = await client.GetAsync($"{_apiBaseUrl}/port/v1/balances/me", ct);
			var balanceData = await balanceResponse.Content.ReadAsStringAsync(ct);

			if (!balanceResponse.IsSuccessStatusCode)
			{
				_logger.LogError("[SAXO-AUTH] Could not fetch balance. Status: {status}, Body: {body}",
					(int)balanceResponse.StatusCode, balanceData);
				throw new HttpRequestException("Could not fetch balance from Saxo.");
			}

			return JsonSerializer.Deserialize<SaxoBalanceResponse>(balanceData, JsonOptions)
			?? throw new InvalidOperationException("Empty balance response from Saxo.");
		}

		public async Task<SaxoTransactionsResponse> GetSaxoTransactionsAsync(string accessToken, DateTime fromDate, DateTime toDate, CancellationToken ct)
		{
			var client = _httpClientFactory.CreateClient();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
			var clientKey = await GetClientKeyAsync(accessToken, ct);
			var transactionsResponse = await client.GetAsync(
				$"{_apiBaseUrl}" +
				$"/hist/v1/transactions?" +
				$"ClientKey={clientKey}&" +
				$"FromDate={fromDate:yyyy-MM-dd}&" +
				$"ToDate={toDate:yyyy-MM-dd}&" +
				$"TransactionType=CashTransfer",
				ct);
			var transactionsData = await transactionsResponse.Content.ReadAsStringAsync(ct);
			if (!transactionsResponse.IsSuccessStatusCode)
			{
				_logger.LogError("[SAXO-AUTH] Could not fetch transactions. Status: {status}, Body: {body}",
					(int)transactionsResponse.StatusCode, transactionsData);
				throw new HttpRequestException("Could not fetch transactions from Saxo.");
			}

			return JsonSerializer.Deserialize<SaxoTransactionsResponse>(transactionsData, JsonOptions)
			?? throw new InvalidOperationException("Empty transactions response from Saxo.");
		}

		public async Task<List<SaxoInstrument>> GetNetPositionsAsync(string accessToken, CancellationToken ct)
		{
			var client = _httpClientFactory.CreateClient();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

			var clientKey = await GetClientKeyAsync(accessToken, ct);

			var response = await client.GetAsync(
				$"{_apiBaseUrl}/port/v1/netpositions?ClientKey={clientKey}", ct);

			response.EnsureSuccessStatusCode();

			var json = await response.Content.ReadAsStringAsync(ct);

			var netPositions = JsonSerializer.Deserialize<SaxoNetPositionsResponse>(json, JsonOptions)
				?? throw new InvalidOperationException("Empty net positions response");

			var apiPositions = netPositions.Data
				.Where(p => p?.NetPositionBase != null)
				.Select(p => p.NetPositionBase)
				.ToList();

			var apiUics = apiPositions.Select(p => p.Uic);

			try
			{
				var savedPositions = await _saxoNetPositionStore.GetNetPositionsAsync();

				var dbUics = savedPositions.Select(p => p.Uic);

				bool isEqual = apiUics.OrderBy(x => x)
									  .SequenceEqual(dbUics.OrderBy(x => x));

				if (isEqual)
					return savedPositions;

				_logger.LogInformation(
					"Net positions have changed, fetching instrument details from API. API UICs: {apiUics}",
					string.Join(", ", apiUics));

				var tasks = apiPositions
					.Select(p => GetInstrumentDetails(client, p.Uic, p.AssetType, ct));

				var instruments = (await Task.WhenAll(tasks)).ToList();

				await _saxoNetPositionStore.UpsertPositionsAsync(instruments);
				await _saxoNetPositionStore.RemoveStalePositionsAsync(instruments.Select(i => i.Uic).ToList());

				return instruments;
			}
			catch (Exception e)
			{
				_logger.LogError(e, "Error while fetching net positions or instrument details. Returning empty list.");
				return [];
			}
		}

		private async Task<SaxoInstrument> GetInstrumentDetails(HttpClient client, int uic, string assetType, CancellationToken ct)
		{
			var response = await client.GetAsync(
				$"{_apiBaseUrl}/ref/v1/instruments/details/{uic}/{assetType}", ct);

			response.EnsureSuccessStatusCode();

			var json = await response.Content.ReadAsStringAsync(ct);

			var instrument = JsonSerializer.Deserialize<SaxoInstrument>(json, JsonOptions)
				?? throw new InvalidOperationException("Empty instrument response");

			instrument.Uic = uic;

			return instrument;
		}

		private async Task<string?> GetClientKeyAsync(string accessToken, CancellationToken ct)
		{
			if (_clientKey is not null)
				return _clientKey;

			var client = _httpClientFactory.CreateClient();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
			var response = await client.GetAsync($"{_apiBaseUrl}/port/v1/clients/me", ct);
			var json = await response.Content.ReadAsStringAsync(ct);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError("[SAXO-AUTH] Could not fetch client key. Status: {status}, Body: {body}",
					(int)response.StatusCode, json);
				throw new HttpRequestException("Could not fetch client key from Saxo.");
			}

			using var doc = JsonDocument.Parse(json);
			_clientKey = doc.RootElement.GetProperty("ClientKey").GetString()
				?? throw new InvalidOperationException("ClientKey missing in Saxo /clients/me response.");
			return _clientKey;
		}
	}
}
