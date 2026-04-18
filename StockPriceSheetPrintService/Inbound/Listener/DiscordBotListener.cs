using Discord;
using Discord.WebSocket;

namespace StockPriceSheetPrintService.Inbound.Listener
{
	public class DiscordBotListener : IHostedService
	{

		private readonly DiscordSocketClient _client;
		private readonly HttpClient _http;
		private readonly string _token;

		public DiscordBotListener(IHttpClientFactory httpFactory, IConfiguration config)
		{
			_client = new DiscordSocketClient(new DiscordSocketConfig
			{
				GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
			});
			_http = httpFactory.CreateClient();
			_token = config["Discord:BotToken"] ?? throw new InvalidOperationException("Discord:BotToken mangler");
		}

		public async Task StartAsync(CancellationToken ct)
		{
			_client.MessageReceived += OnMessageReceived;
			await _client.LoginAsync(TokenType.Bot, _token);
			await _client.StartAsync();
		}
		public async Task StopAsync(CancellationToken ct)
		{
			await _client.StopAsync();
		}

		private async Task OnMessageReceived(SocketMessage msg)
		{
			if (msg.Author.IsBot) return;
			if (msg.Content.Trim() != "!refreshToken") return;
			if (msg.Channel.Id != 1495010067538247880) return;

			try
			{
				var response = await _http.PostAsync("http://192.168.1.239:5151/saxo/refreshToken", null);
				if (response.IsSuccessStatusCode)
					await msg.Channel.SendMessageAsync("✅ Token refreshed.");
				else
					await msg.Channel.SendMessageAsync($"❌ Fejl: {response.StatusCode}");
			}
			catch (Exception ex)
			{
				await msg.Channel.SendMessageAsync($"❌ Exception: {ex.Message}");
			}
		}
	}
}
