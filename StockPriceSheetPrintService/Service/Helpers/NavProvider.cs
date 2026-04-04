using HtmlAgilityPack;
using StockPriceSheetPrintService.Service.Models;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StockPriceSheetPrintService.Service.Helpers
{
	public class NavProvider(HttpClient client)
	{
		public async Task<JuneData?> GetJuneNavAsync(string url, CancellationToken token)
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

			return new JuneData { Nav = nav, Date = date };
		}

		public async Task<JuneData?> GetFromYahooApiAsync(string ticker, CancellationToken token)
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

			return new JuneData { Nav = price, Date = date };
		}
	}
}
