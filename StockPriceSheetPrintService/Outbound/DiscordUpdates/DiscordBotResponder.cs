using Discord;
using Discord.WebSocket;
using StockPriceSheetPrintService.InboundDto;

namespace StockPriceSheetPrintService.Outbound.DiscordUpdates
{
	public class DiscordBotResponder(DiscordSocketClient client) : IDiscordBotResponder
	{
		private ulong? _lastMenuBotMessageId;
		private ulong? _lastHelpBotMessageId;
		private ulong? _lastUpdateBotMessageId;
		private ulong? _lastGetBotMessageId;

		public async Task SendTextAsync(ulong channelId, string text, CancellationToken ct = default)
		{
			if (client.GetChannel(channelId) is not IMessageChannel channel) return;
			await channel.SendMessageAsync(text);
		}

		public async Task SendMenuAsync(ulong channelId, MenuBotResponse response, ulong userMessageId, CancellationToken ct = default)
		{
			if (client.GetChannel(channelId) is not IMessageChannel channel) return;

			if (_lastMenuBotMessageId.HasValue)
			{
				try { await channel.DeleteMessageAsync(_lastMenuBotMessageId.Value); }
				catch { /* Already deleted or missing */ }
			}

			try { await channel.DeleteMessageAsync(userMessageId); }
			catch { /* Already deleted */ }

			var components = new ComponentBuilder();
			foreach (var button in response.Buttons)
				components.WithButton(button.Label, customId: button.CustomId, MapStyle(button.Style));

			var botMessage = await channel.SendMessageAsync(response.Text, components: components.Build());
			_lastMenuBotMessageId = botMessage.Id;
		}

		public async Task SendHelpAsync(ulong channelId, string text, ulong userMessageId, CancellationToken ct = default)
		{
			if (client.GetChannel(channelId) is not IMessageChannel channel) return;

			if (_lastHelpBotMessageId.HasValue)
			{
				try { await channel.DeleteMessageAsync(_lastHelpBotMessageId.Value); }
				catch { /* Already deleted or missing */ }
			}

			try { await channel.DeleteMessageAsync(userMessageId); }
			catch { /* Already deleted */ }

			var botMessage = await channel.SendMessageAsync(text);
			_lastHelpBotMessageId = botMessage.Id;
		}

		public async Task SendUpdateAsync(ulong channelId, UpdateBotResponse response, ulong userMessageId, CancellationToken ct = default)
		{
			if (client.GetChannel(channelId) is not IMessageChannel channel) return;

			if (_lastUpdateBotMessageId.HasValue)
			{
				try { await channel.DeleteMessageAsync(_lastUpdateBotMessageId.Value); }
				catch { /* Already deleted or missing */ }
			}

			try { await channel.DeleteMessageAsync(userMessageId); }
			catch { /* Already deleted */ }

			var components = new ComponentBuilder();
			foreach (var button in response.Buttons)
				components.WithButton(button.Label, customId: button.CustomId, MapStyle(button.Style));

			var botMessage = await channel.SendMessageAsync(response.Text, components: components.Build());
			_lastUpdateBotMessageId = botMessage.Id;
		}

		public async Task SendGetAsync(ulong channelId, GetBotResponse response, ulong userMessageId, CancellationToken ct = default)
		{
			if (client.GetChannel(channelId) is not IMessageChannel channel) return;

			if (_lastGetBotMessageId.HasValue)
			{
				try { await channel.DeleteMessageAsync(_lastGetBotMessageId.Value); }
				catch { /* Already deleted or missing */ }
			}

			try { await channel.DeleteMessageAsync(userMessageId); }
			catch { /* Already deleted */ }

			var components = new ComponentBuilder();
			foreach (var button in response.Buttons)
				components.WithButton(button.Label, customId: button.CustomId, MapStyle(button.Style));

			var botMessage = await channel.SendMessageAsync(response.Text, components: components.Build());
			_lastGetBotMessageId = botMessage.Id;
		}

		private static ButtonStyle MapStyle(BotButtonStyle style) => style switch
		{
			BotButtonStyle.Secondary => ButtonStyle.Secondary,
			BotButtonStyle.Action    => ButtonStyle.Success,
			_                        => ButtonStyle.Primary
		};
	}
}
