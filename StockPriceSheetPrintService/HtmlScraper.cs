using System.Globalization;
using System.Xml;
using HtmlAgilityPack;
using StockPrizeSenderService.Models;

namespace StockPrizeSenderService
{
	public class HtmlScraper
	{
		public async Task<FundPrice?> GetFundNavAsync(string url, CancellationToken token)
		{
			var client = new HttpClient();
			client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

			var html = await client.GetStringAsync(url, token);

			var doc = new HtmlDocument();
			doc.LoadHtml(html);

			// Find alle fund-number divs
			var fundNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'fund-number')]");

			if (fundNodes == null) return null;

			foreach (var node in fundNodes)
			{
				var smallNode = node.SelectSingleNode(".//small[contains(@class,'description')]");
				if (smallNode == null) continue;

				var smallText = smallNode.InnerText.Trim();

				// Tjek om det er NAV feltet
				if (!smallText.StartsWith("Indre værdi pr")) continue;

				// Hent nav
				var valueNode = node.SelectSingleNode(".//p[contains(@class,'value')]");
				if (valueNode == null) continue;

				var navText = valueNode.InnerText.Trim().Replace(",", "."); // dansk komma til punktum
				if (!decimal.TryParse(navText, NumberStyles.Number, CultureInfo.InvariantCulture, out var nav))
					continue;

				// Hent dato fra teksten fx "Indre værdi pr. 11.02.2026"
				var datePart = smallText.Replace("Indre værdi pr.", "").Trim();
				if (!DateTime.TryParseExact(datePart, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
					continue;

				return new FundPrice
				{
					Nav = nav,
					Date = date
				};
			}

			return null; // Hvis ikke fundet
		}
	}
}
