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
			if (modalInteraction.Data.CustomId != "june_modal") return;
			var input_navn = modalInteraction.Data.Components.FirstOrDefault(c => c.CustomId == "input_navn")?.Value;
			var input_besked = modalInteraction.Data.Components.FirstOrDefault(c => c.CustomId == "input_besked")?.Value;
			await modalInteraction.RespondAsync($"Du indtastede: {input_navn} og beskeden: {input_besked}", ephemeral: true);
		}

		private async Task OnBtnExecuted(SocketMessageComponent interaction)
		{
			switch (interaction.Data.CustomId)
			{
				case "btn_klik":
					await interaction.RespondAsync("Du klikkede den blå!", ephemeral: true);
					break;
				case "btn_farlig":
					await interaction.RespondAsync("Du klikkede den røde!", ephemeral: true);
					break;
			}
			var modal = new ModalBuilder()
				.WithTitle("Choose number")
				.WithCustomId("june_modal")
				.AddTextInput("Det nye tal", customId: "input_navn", placeholder: "1234,00")
				.AddTextInput("Besked", customId: "input_besked",
				style: TextInputStyle.Paragraph,
				placeholder: "Skriv din besked...",
				required: false)
				.Build();

			await interaction.RespondWithModalAsync(modal);
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
