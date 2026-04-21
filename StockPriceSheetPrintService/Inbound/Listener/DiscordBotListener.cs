using Discord;
using Discord.WebSocket;
using StockPriceSheetPrintService.Service.Ports.Inbound;

namespace StockPriceSheetPrintService.Inbound.Listener
{
	public class DiscordBotListener(IDiscordBotMessageReceiver discordBotMessageReciver, IConfiguration configuration) : IHostedService
	{

		private readonly DiscordSocketClient _client = new DiscordSocketClient(new DiscordSocketConfig
		{
			GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
		});
		private readonly IDiscordBotMessageReceiver _botMessageReciver = discordBotMessageReciver;
		private readonly string _token = configuration["Discord:BotToken"] ?? throw new InvalidOperationException("Discord:BotToken is missing");
		private CancellationToken _stoppingToken;

		public async Task StartAsync(CancellationToken ct)
		{
			_stoppingToken = ct;
			_client.MessageReceived += OnMessageReceived;
			_client.ButtonExecuted += OnBtnExecuted;
			_client.ModalSubmitted += OnModalSubmitted;
			await _client.LoginAsync(TokenType.Bot, _token);
			await _client.StartAsync();
		}
		public async Task StopAsync(CancellationToken ct)
		{
			await _client.StopAsync();
			_client.Dispose();
		}

		private async Task OnModalSubmitted(SocketModal modalInteraction)
		{
			if (modalInteraction == null) return;
			if (modalInteraction.Channel.Id != 1495010067538247880) return;

			var reply = await _botMessageReciver.DispatchModalAsync(modalInteraction, _stoppingToken);

			if (!string.IsNullOrEmpty(reply))
				await modalInteraction.RespondAsync(reply, ephemeral: true);
		}

		private async Task OnBtnExecuted(SocketMessageComponent interaction)
		{
			if (interaction == null) return;
			if (interaction.Channel.Id != 1495010067538247880) return;

			var reply = await _botMessageReciver.DispatchMessageComponentAsync(interaction, _stoppingToken);
			if (reply != null)
			{
				await interaction.RespondWithModalAsync(reply);
			}
		}

		private async Task OnMessageReceived(SocketMessage msg)
		{
			if (msg.Author.IsBot) return;
			if (msg.Channel.Id != 1495010067538247880) return;
			
			var reply = await _botMessageReciver.DispatchMessageAsync(msg, _stoppingToken);
			if (!string.IsNullOrEmpty(reply))
				await msg.Channel.SendMessageAsync(reply);
		}
	}
}
