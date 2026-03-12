namespace StockPriceSheetPrintService.DiscordUpdates
{
	public class DiscordNotifier
	{
		private readonly HttpClient _httpClient;
		private readonly string _webhookUrl;

		public DiscordNotifier(HttpClient httpClient, IConfiguration config)
		{
			_httpClient = httpClient;
			_webhookUrl = config["Discord:WebhookUrl"] ?? string.Empty;
		}

		public async Task SendMorningReportAsync(decimal saxoBalance, decimal stockValue, decimal fundValue, decimal total, CancellationToken stoppingToken)
		{
			var payload = BuildPayload(saxoBalance, stockValue, fundValue, total);
			await _httpClient.PostAsJsonAsync(_webhookUrl, payload, cancellationToken: stoppingToken);
		}

		private object BuildPayload(decimal saxoBalance, decimal stockValue, decimal fundValue, decimal total)
		{
			return new
			{
				embeds = new object[]
				{
					// EMBED 1
					new
					{
						title = "🌅 Morning Market Report",
						description = DateTime.Now.ToString("dddd dd MMMM yyyy"),
						color = 3447003,
						fields = new[]
						{
							new { name = "📈 Portfolio", value = "```" + $"Saxo   {saxoBalance,12:F2} DKK\nStocks {stockValue,12:F2} DKK\nFunds  {fundValue,12:F2} DKK\n```", inline = false },
							new { name = "💰 Total Value", value = $"**{total:F2} DKK**", inline = false }
						},
						timestamp = DateTime.UtcNow
					},
					// EMBED 2
					new
					{
						title = "📊 Portfolio Stats",
						color = 10181046,
						fields = new[]
						{
							new { name = "Saxo %", value = $"{Math.Round((saxoBalance/total)*100,1)}%", inline = true },
							new { name = "Stocks %", value = $"{Math.Round((stockValue/total)*100,1)}%", inline = true },
							new { name = "Funds %", value = $"{Math.Round((fundValue/total)*100,1)}%", inline = true }
						}
					}
				}
			};
		}
	}
}
