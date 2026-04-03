using HtmlAgilityPack;
using StockPriceSheetPrintService.Service.Models;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StockPriceSheetPrintService.Service.Helpers
{
	public class FundNavClient(HttpClient client)
	{
		public async Task<FundPrice?> GetFundNavAsync(string url, CancellationToken token)
		{
			var html = await client.GetStringAsync(url, token);

			var doc = new HtmlDocument();
			doc.LoadHtml(html);

			var valueNode = doc.DocumentNode.SelectSingleNode(
				"//div[contains(@class,'fund-number')]" +
				"[.//small[contains(@class,'description') and starts-with(normalize-space(.), 'Indre værdi pr')]]" +
				"//p[contains(@class,'value')]"
			);

			if (valueNode == null) return null;

			var navText = valueNode.InnerText.Trim().Replace(",", ".");
			if (!decimal.TryParse(navText, NumberStyles.Number, CultureInfo.InvariantCulture, out var nav))
				return null;

			var smallNode = doc.DocumentNode.SelectSingleNode(
				"//div[contains(@class,'fund-number')]" +
				"//small[contains(@class,'description') and starts-with(normalize-space(.), 'Indre værdi pr')]"
			);

			var dateMatch = Regex.Match(smallNode?.InnerText ?? "", @"\d{2}\.\d{2}\.\d{4}");
			if (!dateMatch.Success) return null;

			if (!DateTime.TryParseExact(dateMatch.Value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
				return null;

			return new FundPrice { Nav = nav, Date = date };
		}

		public async Task<FundPrice?> GetFromYahooApiAsync(string ticker, CancellationToken token)
		{
			var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{ticker}";
			var json = await client.GetStringAsync(url, token);

			using var doc = JsonDocument.Parse(json);
			var result = doc.RootElement
				.GetProperty("chart")
				.GetProperty("result")[0];

			var price = result
				.GetProperty("meta")
				.GetProperty("regularMarketPrice")
				.GetDecimal();

			var timestamp = result
				.GetProperty("meta")
				.GetProperty("regularMarketTime")
				.GetInt64();

			var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;

			return new FundPrice { Nav = price, Date = date };
		}
	}
}
