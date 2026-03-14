using System.Globalization;

namespace StockPriceSheetPrintService.DiscordUpdates
{
	public class DiscordNotifier
	{
		private readonly HttpClient _httpClient;
		private readonly IConfiguration _configuration;
		private readonly string _webhookUrl;

		public DiscordNotifier(HttpClient httpClient, IConfiguration configuration)
		{
			_httpClient = httpClient;
			_configuration = configuration;
			_webhookUrl = _configuration["Discord:Webhook"] ?? string.Empty;
		}

		public async Task SendMorningReportAsync(decimal saxoBalance, decimal stockValue, decimal fundValue, decimal total, decimal dayBeforeValue, CancellationToken stoppingToken)
		{
			var payload = BuildPayload(saxoBalance, stockValue, fundValue, total, dayBeforeValue);
			await _httpClient.PostAsJsonAsync(_webhookUrl, payload, cancellationToken: stoppingToken);
		}

		static string Dkk(decimal value)
		{
			return value.ToString("N2", CultureInfo.GetCultureInfo("da-DK"));
		}

		private object BuildPayload(decimal saxoBalance, decimal stockValue, decimal fundValue, decimal total, decimal dayBeforeValue)
		{
			var change = total - dayBeforeValue;
			var changePct = Math.Round((change / dayBeforeValue) * 100, 2);
			var sign = change >= 0 ? "+" : "";
			var embedColor = change >= 0 ? 3066993 : 15158332;
			var changeSinceYesterdayString = change >= 0 ? "📈 Change Since Yesterday" : "📉 Change Since Yesterday";

			var portfolioValue = "```" + $"Saxo    {Dkk(saxoBalance),12} DKK\nNordnet {Dkk(stockValue),12} DKK\nJune    {Dkk(fundValue),12} DKK\n" + "```";
			var changeValue = "```diff\n" + $"{sign}{Dkk(change)} DKK ({sign}{changePct}%)" + "\n```";
			var distributionValue = "```" + $"Saxo    {Math.Round((saxoBalance / total) * 100, 1),6}%\nNordnet {Math.Round((stockValue / total) * 100, 1),6}%\nJune    {Math.Round((fundValue / total) * 100, 1),6}%" + "```";

			return new
			{
				embeds = new object[]
				{
					new
					{
						title = "🌅 Morning Market Report",
						description = "For " + DateTime.Now.AddDays(-1).ToString("dddd dd MMMM yyyy"),
						color = embedColor,
						fields = new[]
						{
							new { name = "🏛️ Portfolio", value = portfolioValue, inline = false },
							new { name = "💰 Total Value", value = $"**{Dkk(total)} DKK**", inline = false },
							new { name = changeSinceYesterdayString, value = changeValue, inline = false }
						},
						timestamp = DateTime.UtcNow
					},
					new
					{
						title = "📊 Portfolio Stats",
						color = 10181046,
						fields = new[]
						{
							new { name = "Distribution", value = distributionValue, inline = false }
						}
					}
				}
			};
		}
	}
}
