using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports.Outbound;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockPriceSheetPrintService.Outbound.MarketStack
{
	public class MarketStackService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<MarketStackService> logger) : IMarketStackService
	{
		private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
		private readonly IConfiguration _configuration = configuration;
		private readonly ILogger<MarketStackService> _logger = logger;

		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
			Converters = { new FlexibleDateTimeOffsetConverter() }
		};

		public class FlexibleDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
		{
			private static readonly string[] Formats =
			{
			"yyyy-MM-dd'T'HH:mm:sszzz",
			"yyyy-MM-dd'T'HH:mm:sszz"
		};

			public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			{
				var raw = reader.GetString()!;
				return DateTimeOffset.ParseExact(raw, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None);
			}

			public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
				=> writer.WriteStringValue(value.ToString("yyyy-MM-dd'T'HH:mm:sszzz"));
		}

		public async Task<EodResponse?> GetStockPricesAsync(CancellationToken ct)
		{
			var client = _httpClientFactory.CreateClient("StockApi");
			var symbolsQuery = string.Join(",", AllTickers.Symbols.Keys);

			var response = await client.GetAsync(
				$"v2/eod/latest?access_key={_configuration["StockApi:AccessKey"]}&symbols={symbolsQuery}", ct);

			if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
			{
				response = await client.GetAsync(
					$"v2/eod/latest?access_key={_configuration["StockApi:AccessKey2"]}&symbols={symbolsQuery}", ct);
			}

			response.EnsureSuccessStatusCode();

			var json = await response.Content.ReadAsStringAsync(ct);
			_logger.LogInformation("[API-DEBUG] Marketstack response: {json}", json);

			using var doc = JsonDocument.Parse(json);
			if (!doc.RootElement.TryGetProperty("data", out var dataEl))
			{
				_logger.LogError("[API-DEBUG] Uventet JSON-struktur fra Marketstack: {json}",
					json.Substring(0, Math.Min(500, json.Length)));
				return null;
			}

			var data = JsonSerializer.Deserialize<List<EodDatum>>(dataEl.GetRawText(), JsonOptions);
			return new EodResponse { Data = data ?? [] };
		}
	}
}
