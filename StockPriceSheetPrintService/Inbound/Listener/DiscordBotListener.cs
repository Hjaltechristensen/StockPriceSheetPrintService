using Discord;
using Discord.WebSocket;
using Serilog.Context;
using StockPriceSheetPrintService.Inbound.Mappers;
using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports.Inbound;

namespace StockPriceSheetPrintService.Inbound.Listener
{
	public class DiscordBotListener(
		DiscordSocketClient client,
		IDiscordBotMessageReceiver receiver,
		IConfiguration configuration) : IHostedService
	{
		private const ulong ChannelId = 1495010067538247880;
		private readonly string _token = configuration["Discord:BotToken"] ?? throw new InvalidOperationException("Discord:BotToken is missing");
		private CancellationToken _stoppingToken;

		private ulong? _lastMenuMessageId;
		private ulong? _lastHelpMessageId;
		private ulong? _lastUpdateMessageId;
		private ulong? _lastGetMessageId;

		public async Task StartAsync(CancellationToken ct)
		{
			_stoppingToken = ct;
			client.MessageReceived += OnMessageReceived;
			client.ButtonExecuted += OnButtonExecuted;
			client.ModalSubmitted += OnModalSubmitted;
			await client.LoginAsync(TokenType.Bot, _token);
			await client.StartAsync();
		}

		public async Task StopAsync(CancellationToken ct) => await client.StopAsync();

		private async Task OnMessageReceived(SocketMessage msg)
		{
			if (msg.Author.IsBot) return;
			if (msg.Channel.Id != ChannelId) return;

			var ctx = ClientContextFactory.New("Discord:message");
			using var _1 = LogContext.PushProperty("CorrelationId", ctx.CorrelationId);
			using var _2 = LogContext.PushProperty("Source", ctx.Source);

			var dto = BotMapper.ToCommandDto(msg);
			var command = BotMapper.ToDomain(dto);
			var response = await receiver.HandleMessageAsync(command, ctx, _stoppingToken);
			await SendResponseToChannel(msg.Channel.Id, response, msg.Id);
		}

		private async Task OnButtonExecuted(SocketMessageComponent interaction)
		{
			if (interaction.Channel.Id != ChannelId) return;

			var ctx = ClientContextFactory.New("Discord:button");
			using var _1 = LogContext.PushProperty("CorrelationId", ctx.CorrelationId);
			using var _2 = LogContext.PushProperty("Source", ctx.Source);

			var dto = BotMapper.ToComponentDto(interaction);
			var command = BotMapper.ToDomain(dto);
			var response = await receiver.HandleComponentAsync(command, ctx, _stoppingToken);

			switch (response)
			{
				case ModalBotResponse modal:
					await interaction.RespondWithModalAsync(BuildModal(modal));
					break;
				case TextBotResponse text:
					await interaction.RespondAsync(text.Text, ephemeral: text.Ephemeral);
					break;
				case HelpBotResponse help:
					await interaction.RespondAsync(help.Text, ephemeral: true);
					break;
				case GetBotResponse get:
					await interaction.RespondAsync(get.Text, components: BuildComponents(get.Buttons), ephemeral: false);
					try { await interaction.Message.DeleteAsync(); } catch { /* already deleted */ }
					break;
				case UpdateBotResponse update:
					await interaction.RespondAsync(update.Text, components: BuildComponents(update.Buttons), ephemeral: false);
					try { await interaction.Message.DeleteAsync(); } catch { /* already deleted */ }
					break;
				case MenuBotResponse menu:
					await interaction.UpdateAsync(props =>
					{
						props.Content = menu.Text;
						props.Components = BuildComponents(menu.Buttons);
					});
					break;
			}
		}

		private async Task OnModalSubmitted(SocketModal modalInteraction)
		{
			if (modalInteraction.Channel.Id != ChannelId) return;

			var ctx = ClientContextFactory.New("Discord:modal");
			using var _1 = LogContext.PushProperty("CorrelationId", ctx.CorrelationId);
			using var _2 = LogContext.PushProperty("Source", ctx.Source);

			var dto = BotMapper.ToModalDto(modalInteraction);
			var command = BotMapper.ToDomain(dto);
			var response = await receiver.HandleModalAsync(command, ctx, _stoppingToken);

			if (response is TextBotResponse text)
				await modalInteraction.RespondAsync(text.Text, ephemeral: text.Ephemeral);
		}

		private async Task SendResponseToChannel(ulong channelId, BotResponse response, ulong sourceMessageId)
		{
			if (client.GetChannel(channelId) is not IMessageChannel channel) return;

			switch (response)
			{
				case TextBotResponse text:
					await channel.SendMessageAsync(text.Text);
					break;

				case MenuBotResponse menu:
					await DeleteTrackedMessage(channel, _lastMenuMessageId);
					try { await channel.DeleteMessageAsync(sourceMessageId); } catch { /* already deleted */ }
					var menuMsg = await channel.SendMessageAsync(menu.Text, components: BuildComponents(menu.Buttons));
					_lastMenuMessageId = menuMsg.Id;
					break;

				case HelpBotResponse help:
					await DeleteTrackedMessage(channel, _lastHelpMessageId);
					try { await channel.DeleteMessageAsync(sourceMessageId); } catch { /* already deleted */ }
					var helpMsg = await channel.SendMessageAsync(help.Text);
					_lastHelpMessageId = helpMsg.Id;
					break;

				case UpdateBotResponse update:
					await DeleteTrackedMessage(channel, _lastUpdateMessageId);
					try { await channel.DeleteMessageAsync(sourceMessageId); } catch { /* already deleted */ }
					var updateMsg = await channel.SendMessageAsync(update.Text, components: BuildComponents(update.Buttons));
					_lastUpdateMessageId = updateMsg.Id;
					break;

				case GetBotResponse get:
					await DeleteTrackedMessage(channel, _lastGetMessageId);
					try { await channel.DeleteMessageAsync(sourceMessageId); } catch { /* already deleted */ }
					var getMsg = await channel.SendMessageAsync(get.Text, components: BuildComponents(get.Buttons));
					_lastGetMessageId = getMsg.Id;
					break;
			}
		}

		private static async Task DeleteTrackedMessage(IMessageChannel channel, ulong? messageId)
		{
			if (messageId.HasValue)
				try { await channel.DeleteMessageAsync(messageId.Value); } catch { /* already deleted */ }
		}

		private static Modal BuildModal(ModalBotResponse modal)
		{
			var builder = new ModalBuilder().WithTitle(modal.Title).WithCustomId(modal.ModalId);
			foreach (var field in modal.Fields)
				builder.AddTextInput(field.Label, customId: field.CustomId, placeholder: field.Placeholder);
			return builder.Build();
		}

		private static MessageComponent BuildComponents(List<BotButton> buttons)
		{
			var builder = new ComponentBuilder();
			foreach (var button in buttons)
				builder.WithButton(button.Label, customId: button.CustomId, MapStyle(button.Style));
			return builder.Build();
		}

		private static ButtonStyle MapStyle(BotButtonStyle style) => style switch
		{
			BotButtonStyle.Secondary => ButtonStyle.Secondary,
			BotButtonStyle.Action    => ButtonStyle.Success,
			_                        => ButtonStyle.Primary
		};
	}
}
