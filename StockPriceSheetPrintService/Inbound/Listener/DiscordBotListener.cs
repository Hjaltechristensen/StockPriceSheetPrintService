using Discord;
using Discord.WebSocket;
using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports.Inbound;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Inbound.Listener
{
	public class DiscordBotListener(
		DiscordSocketClient client,
		IDiscordBotMessageReceiver receiver,
		IDiscordBotResponder responder,
		IConfiguration configuration) : IHostedService
	{
		private const ulong ChannelId = 1495010067538247880;
		private readonly string _token = configuration["Discord:BotToken"] ?? throw new InvalidOperationException("Discord:BotToken is missing");
		private CancellationToken _stoppingToken;

		public async Task StartAsync(CancellationToken ct)
		{
			_stoppingToken = ct;
			client.MessageReceived += OnMessageReceived;
			client.ButtonExecuted += OnButtonExecuted;
			client.ModalSubmitted += OnModalSubmitted;
			await client.LoginAsync(TokenType.Bot, _token);
			await client.StartAsync();
		}

		public async Task StopAsync(CancellationToken ct)
		{
			await client.StopAsync();
		}

		private async Task OnMessageReceived(SocketMessage msg)
		{
			if (msg.Author.IsBot) return;
			if (msg.Channel.Id != ChannelId) return;

			var parts = msg.Content.Split(' ');
			var command = new BotMessageCommand(parts[0], parts[1..], msg.Channel.Id);
			var response = await receiver.HandleMessageAsync(command, _stoppingToken);
			await SendResponse(msg.Channel.Id, response, msg.Id);
		}

		private async Task OnButtonExecuted(SocketMessageComponent interaction)
		{
			if (interaction.Channel.Id != ChannelId) return;

			var command = new BotComponentCommand(interaction.Data.CustomId);
			var response = await receiver.HandleComponentAsync(command, _stoppingToken);

			if (response is ModalBotResponse modal)
				await interaction.RespondWithModalAsync(BuildModal(modal));
		}

		private async Task OnModalSubmitted(SocketModal modalInteraction)
		{
			if (modalInteraction.Channel.Id != ChannelId) return;

			var fields = modalInteraction.Data.Components.ToDictionary(c => c.CustomId, c => c.Value);
			var command = new BotModalCommand(modalInteraction.Data.CustomId, fields);
			var response = await receiver.HandleModalAsync(command, _stoppingToken);

			if (response is TextBotResponse text)
				await modalInteraction.RespondAsync(text.Text, ephemeral: text.Ephemeral);
		}

		private async Task SendResponse(ulong channelId, BotResponse response, ulong sourceMessageId = 0)
		{
			switch (response)
			{
				case TextBotResponse text:
					await responder.SendTextAsync(channelId, text.Text, _stoppingToken);
					break;
				case ComponentsBotResponse components:
					await responder.SendComponentsAsync(channelId, components, _stoppingToken);
					break;
				case HelpBotResponse help:
					await responder.SendHelpAsync(channelId, help.Text, sourceMessageId, _stoppingToken);
					break;
				case UpdateBotResponse update:
					await responder.SendUpdateAsync(channelId, update, sourceMessageId, _stoppingToken);
					break;
			}
		}

		private static Modal BuildModal(ModalBotResponse modal)
		{
			var builder = new ModalBuilder()
				.WithTitle(modal.Title)
				.WithCustomId(modal.ModalId);
			foreach (var field in modal.Fields)
				builder.AddTextInput(field.Label, customId: field.CustomId, placeholder: field.Placeholder);
			return builder.Build();
		}
	}
}
