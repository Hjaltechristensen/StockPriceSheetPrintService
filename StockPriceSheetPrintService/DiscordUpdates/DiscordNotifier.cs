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
						description = DateTime.Now.AddDays(-1).ToString("dddd dd MMMM yyyy"),
						color = 3447003,
						fields = new[]
						{
							new { name = "📈 Portfolio", value = "```" + $"Saxo   {saxoBalance,12:F2} DKK\nNordnet {stockValue,12:F2} DKK\nJune  {fundValue,12:F2} DKK\n```", inline = false },
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
							new { name = "Saxo", value = $"{Math.Round((saxoBalance/total)*100,1)}%", inline = true },
							new { name = "Nordnet", value = $"{Math.Round((stockValue/total)*100,1)}%", inline = true },
							new { name = "June", value = $"{Math.Round((fundValue/total)*100,1)}%", inline = true }
						}
					}
				}
			};
		}
	}
}
