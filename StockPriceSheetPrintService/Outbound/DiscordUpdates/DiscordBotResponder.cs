using Discord;
using Discord.WebSocket;
using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Outbound.DiscordUpdates
{
	public class DiscordBotResponder(DiscordSocketClient client) : IDiscordBotResponder
	{
		private ulong? _lastHelpBotMessageId;

		public async Task SendTextAsync(ulong channelId, string text, CancellationToken ct = default)
		{
			if (client.GetChannel(channelId) is not IMessageChannel channel) return;
			await channel.SendMessageAsync(text);
		}

		public async Task SendComponentsAsync(ulong channelId, ComponentsBotResponse response, CancellationToken ct = default)
		{
			if (client.GetChannel(channelId) is not IMessageChannel channel) return;

			var components = new ComponentBuilder();
			foreach (var button in response.Buttons)
				components.WithButton(button.Label, customId: button.CustomId, ButtonStyle.Primary);

			await channel.SendMessageAsync(response.Text, components: components.Build());
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
	}
}
