using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports.Outbound;
using System.Globalization;
using System.Xml.Linq;

namespace StockPriceSheetPrintService.Service.Application
{
	public class PortfolioCalculator(
		IHttpClientFactory httpClientFactory,
		ILogger<PortfolioCalculator> logger,
		IHtmlScraper htmlScraper,
		IConfiguration configuration,
		IJuneStore juneStore,
		INordnetSymbolStore nordnetSymbolStore) : IPortfolioCalculator
	{
		private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
		private readonly ILogger<PortfolioCalculator> _logger = logger;
		private readonly IHtmlScraper _htmlScraper = htmlScraper;
		private readonly IConfiguration _configuration = configuration;
		private readonly IJuneStore _juneStore = juneStore;
		private readonly INordnetSymbolStore _nordnetSymbolStore = nordnetSymbolStore;
		private const decimal NordnetFxMargin = 0.0025m;
		private Dictionary<string, decimal>? _exchangeRateCache;

		private static readonly Dictionary<string, string> ExchangeCurrencyFallback = new()
		{
			["XETR"] = "EUR",
			["XPAR"] = "EUR",
			["XAMS"] = "EUR",
			["XNAS"] = "USD",
			["XNYS"] = "USD",
			["XLON"] = "GBp",
			["XCSE"] = "DKK",
		};

		public async Task<decimal> CalculateTotalStockValueAsync(EodResponse data, CancellationToken ct)
		{
			decimal totalPrice = 0;
			if (data == null) return 0;

			_exchangeRateCache = null;
			var rates = await GetExchangeRatesAsync(ct);

			var nordnetSymbols = await _nordnetSymbolStore.GetSymbolsAsync();
			foreach (var d in data.Data)
			{
				if (!nordnetSymbols.TryGetValue(d.Symbol, out decimal multiplier))
					continue;

				var effectiveCurrency = !string.IsNullOrEmpty(d.PriceCurrency) ? d.PriceCurrency
					: (ExchangeCurrencyFallback.TryGetValue(d.Exchange ?? "", out var fb) ? fb : "?");

				if (d.Close is null or 0m)
				{
					_logger.LogWarning("Closing price was null/0 for {Symbol} - Redirecting to YahooFinance", d.Symbol);
					var yahooData = await _htmlScraper.GetFromYahooApiAsync(d.Symbol, ct);
					d.Date = yahooData?.Date ?? d.Date;
					d.Close = yahooData?.Nav ?? d.Close ?? 0m;
				}

				var closePrice = d.Close ?? 0m;
				var priceInDkk = ConvertCurrencyToDkk(closePrice, d.PriceCurrency, d.Exchange, rates);
				_logger.LogInformation("[JOB] {Multiplier} x {Symbol} closed at: {Close} {Currency} = {Dkk:F4} DKK, total: {Total:F2} DKK",
					multiplier, d.Symbol, closePrice, effectiveCurrency, priceInDkk, multiplier * priceInDkk);
				totalPrice += priceInDkk * multiplier;
			}

			_logger.LogInformation("[JOB] Total stock value: {totalPrice:F2} DKK", totalPrice);
			return totalPrice;
		}

		public async Task<decimal> FindTotalJuneValueAsync(CancellationToken ct)
		{
			decimal totalJuneValue = 0m;

			var junePrice = await _htmlScraper.GetJuneNavAsync(_configuration["JuneUrl"] ?? string.Empty, ct);
			if (junePrice != null)
			{
				_logger.LogInformation("Todays June price: {nav} pr. {date}", junePrice.Nav, junePrice.Date.ToString("dd/MM/yyyy"));
				var shareAmount = await _juneStore.GetJuneSharesAmountAsync();
				totalJuneValue = junePrice.Nav * shareAmount.Amount;
			}

			return totalJuneValue;
		}

		private decimal ConvertCurrencyToDkk(decimal price, string? currency, string? exchange, Dictionary<string, decimal> rates)
		{
			if (string.IsNullOrEmpty(currency) && !string.IsNullOrEmpty(exchange))
			{
				if (ExchangeCurrencyFallback.TryGetValue(exchange, out var fallback))
				{
					_logger.LogWarning("[CURRENCY] {exchange} has null currency – using fallback: {currency}", exchange, fallback);
					currency = fallback;
				}
			}

			if (string.IsNullOrEmpty(currency))
			{
				_logger.LogWarning("[CURRENCY] Could not determine currency – using rate 1:1");
				return price;
			}

			if (exchange == "XLON" || currency == "GBp" || currency == "GBX")
			{
				if (rates.TryGetValue("GBP", out var gbpRate))
					return (price / 100m) * gbpRate;
			}

			if (rates.TryGetValue(currency, out var rate))
				return price * rate;

			_logger.LogWarning("[CURRENCY] Unknown currency: {currency} – using rate 1:1", currency);
			return price;
		}

		private async Task<Dictionary<string, decimal>> GetExchangeRatesAsync(CancellationToken ct)
		{
			if (_exchangeRateCache != null)
				return _exchangeRateCache;

			var client = _httpClientFactory.CreateClient("NationalbankApi");
			var response = await client.GetAsync("api/currencyratesxml?lang=da", ct);
			response.EnsureSuccessStatusCode();

			var xml = await response.Content.ReadAsStringAsync(ct);
			var doc = XDocument.Parse(xml);

			_exchangeRateCache = new Dictionary<string, decimal> { ["DKK"] = 1m };

			foreach (var c in doc.Descendants("currency"))
			{
				var code = c.Attribute("code")?.Value;
				var rateStr = c.Attribute("rate")?.Value;

				if (code != null && rateStr != null &&
					decimal.TryParse(rateStr, NumberStyles.Any, new CultureInfo("da-DK"), out decimal rate))
				{
					_exchangeRateCache[code] = (rate / 100m) * (1 - NordnetFxMargin);
				}
			}

			_logger.LogInformation("[CURRENCY] Exchange rates including Nordnet margin – USD: {usd:F4} DKK, EUR: {eur:F4} DKK",
				_exchangeRateCache.GetValueOrDefault("USD"),
				_exchangeRateCache.GetValueOrDefault("EUR"));

			return _exchangeRateCache;
		}
	}
}
