using StockPriceSheetPrintService.Service.Ports.Outbound;
using System.Globalization;

namespace StockPriceSheetPrintService.Outbound.DiscordUpdates
{
	public class DiscordNotifier : IDiscordNotifier
	{
		private readonly HttpClient _httpClient;
		private readonly IConfiguration _configuration;
		private readonly ILogger<DiscordNotifier> _logger;
		private readonly string _webhookUrl;
		private readonly string _webhookUrlLogin;

		private const int EmbedColorPositive = 3066993; // Green
		private const int EmbedColorNegative = 15158332; // Red
		private const int EmbedColorStats = 10181046; // Purple

		public DiscordNotifier(HttpClient httpClient, IConfiguration configuration, ILogger<DiscordNotifier> logger)
		{
			_httpClient = httpClient;
			_configuration = configuration;
			_logger = logger;
			_webhookUrl = _configuration["Discord:Webhook"] ?? string.Empty;
			_webhookUrlLogin = _configuration["Discord:WebhookLogin"] ?? string.Empty;

			if (string.IsNullOrEmpty(_webhookUrl))
				_logger.LogWarning("[DISCORD] Discord:Webhook configuration missing");
			if (string.IsNullOrEmpty(_webhookUrlLogin))
				_logger.LogWarning("[DISCORD] Discord:WebhookLogin configuration missing");
		}

		public async Task SendMorningReportAsync(decimal saxoBalance, decimal stockValue, decimal juneValue, decimal total, decimal dayBeforeValue, decimal? lastTransferAmount, string? claudeInsights, CancellationToken stoppingToken)
		{
			var payload = BuildPayload(saxoBalance, stockValue, juneValue, total, dayBeforeValue, lastTransferAmount, claudeInsights);
			await PublishDiscordMessage(_webhookUrl, payload, stoppingToken);
		}

		private async Task PublishDiscordMessage(string webhook, object payload, CancellationToken stoppingToken)
		{
			if (string.IsNullOrWhiteSpace(webhook))
			{
				_logger.LogWarning("[DISCORD] Webhook URL is empty, skipping message");
				return;
			}

			try
			{
				await _httpClient.PostAsJsonAsync(webhook, payload, cancellationToken: stoppingToken);
				_logger.LogInformation("[DISCORD] Message sent successfully");
			}
			catch (HttpRequestException ex)
			{
				_logger.LogError(ex, "[DISCORD] Failed to send message to Discord API");
			}
			catch (OperationCanceledException ex)
			{
				_logger.LogError(ex, "[DISCORD] Message send was cancelled");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[DISCORD] Unexpected error while sending message");
			}
		}

		public async Task SendLoginUrlAsync(string loginUrl, CancellationToken stoppingToken)
		{
			var payload = new
			{
				embeds = new[]
				{
					new
					{
						title = "Saxo token expired",
						description = $"Refresh token has been invalidated — probably because of Saxo maintenance. Manual re-authentication is required.\n\n[**› Log in to Saxo**]({loginUrl})",
						color = 0xED4245,
						fields = new[]
						{
							new { name = "❗VPN Required", value = $"Remember to be on VPN when you login", inline = false }
						},
						timestamp = DateTime.UtcNow.ToString("o")
					}
				}
			};

			await PublishDiscordMessage(_webhookUrlLogin, payload, stoppingToken);
		}

		static string Dkk(decimal value)
		{
			return value.ToString("N2", CultureInfo.GetCultureInfo("da-DK"));
		}

		private object BuildPayload(decimal saxoBalance, decimal stockValue, decimal juneValue, decimal total, decimal dayBeforeValue, decimal? lastTransferAmount, string? claudeInsights)
		{
			var change = total - dayBeforeValue;
			var changePct = Math.Round((change / dayBeforeValue) * 100, 2);
			var sign = change >= 0 ? "+" : "";
			var embedColor = change >= 0 ? EmbedColorPositive : EmbedColorNegative;
			var changeSinceYesterdayString = change >= 0 ? "📈 Change Since Yesterday" : "📉 Change Since Yesterday";

			var portfolioValue = "```" + $"Saxo    {Dkk(saxoBalance),12} DKK\nNordnet {Dkk(stockValue),12} DKK\nJune    {Dkk(juneValue),12} DKK\n" + "```";
			var changeValue = "```diff\n" + $"{sign}{Dkk(change)} DKK ({sign}{changePct}%)" + "\n```";
			if (lastTransferAmount.HasValue)
				changeValue += $"\n*⚠️ Inkluderer seneste indskud på {Dkk(lastTransferAmount.Value)} DKK*";

			var distributionValue = "```" + $"Saxo    {Math.Round((saxoBalance / total) * 100, 1),6}%\nNordnet {Math.Round((stockValue / total) * 100, 1),6}%\nJune    {Math.Round((juneValue / total) * 100, 1),6}%" + "```";

			var mainFields = new List<object>
			{
				new { name = "🏛️ Portfolio", value = $"||{portfolioValue}||", inline = false },
				new { name = "💰 Total Value", value = $"||**{Dkk(total)} DKK**||", inline = false },
				new { name = changeSinceYesterdayString, value = changeValue, inline = false }
			};

			if (!string.IsNullOrWhiteSpace(claudeInsights))
				mainFields.Add(new { name = "🤖 AI Insights", value = claudeInsights, inline = false });

			return new
			{
				embeds = new object[]
				{
					new
					{
						title = "🌅 Morning Market Report",
						description = "For " + DateTime.UtcNow.AddDays(-1).ToString("dddd dd MMMM yyyy"),
						color = embedColor,
						fields = mainFields,
						timestamp = DateTime.UtcNow.ToString("o")
					},
					new
					{
						title = "📊 Portfolio Stats",
						color = EmbedColorStats,
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
