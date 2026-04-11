using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports.Outbound;
using System.Text.Json;

namespace StockPriceSheetPrintService.Outbound.MarketStack
{
	public class MarketStackService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<MarketStackService> logger) : IMarketStackService
	{
		private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
		private readonly IConfiguration _configuration = configuration;
		private readonly ILogger<MarketStackService> _logger = logger;

		private const string StockApiClientName = "StockApi";
		private const string EodLatestEndpoint = "v2/eod/latest";
		private const string PrimaryAccessKeyConfig = "StockApi:AccessKey";
		private const string FallbackAccessKeyConfig = "StockApi:AccessKey2";
		private const string AccessKeyParameter = "access_key";
		private const string SymbolsParameter = "symbols";

		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
			Converters = { new FlexibleDateTimeOffsetConverter() }
		};

		public async Task<EodResponse?> GetStockPricesAsync(CancellationToken ct)
		{
			var client = _httpClientFactory.CreateClient(StockApiClientName);
			var symbolsQuery = string.Join(",", AllTickers.Symbols.Keys);

			var response = await GetStockPricesWithFallbackAsync(client, symbolsQuery, ct);

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError("[MARKETSTACK] Both API keys exhausted or API request failed. Status: {status}",
					(int)response.StatusCode);
				return null;
			}

			var json = await response.Content.ReadAsStringAsync(ct);
			_logger.LogDebug("[MARKETSTACK] API response received successfully");

			try
			{
				var eodResponse = JsonSerializer.Deserialize<EodResponse>(json, JsonOptions);
				if (eodResponse?.Data == null || eodResponse.Data.Count == 0)
				{
					_logger.LogError("[MARKETSTACK] Empty data in API response");
					return null;
				}
				return eodResponse;
			}
			catch (JsonException ex)
			{
				_logger.LogError(ex, "[MARKETSTACK] Failed to deserialize API response");
				return null;
			}
		}

		private async Task<HttpResponseMessage> GetStockPricesWithFallbackAsync(HttpClient client, string symbolsQuery, CancellationToken ct)
		{
			var primaryKey = _configuration[PrimaryAccessKeyConfig] ?? string.Empty;
			if (string.IsNullOrEmpty(primaryKey))
				_logger.LogWarning("[MARKETSTACK] Failed to get primary access key to MarketStack");
			var response = await client.GetAsync(
				BuildQueryUrl(primaryKey, symbolsQuery), ct);

			if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
			{
				_logger.LogInformation("[MARKETSTACK] Primary API key exhausted, trying fallback key");
				var fallbackKey = _configuration[FallbackAccessKeyConfig] ?? string.Empty;
				if (string.IsNullOrEmpty(fallbackKey))
					_logger.LogWarning("[MARKETSTACK] Failed to get fallback access key to MarketStack");
				response = await client.GetAsync(
					BuildQueryUrl(fallbackKey, symbolsQuery), ct);
			}
			return response;
		}

		private string BuildQueryUrl(string accessKey, string symbolsQuery)
		{
			return $"{EodLatestEndpoint}?{AccessKeyParameter}={accessKey}&{SymbolsParameter}={symbolsQuery}";
		}
	}
}
