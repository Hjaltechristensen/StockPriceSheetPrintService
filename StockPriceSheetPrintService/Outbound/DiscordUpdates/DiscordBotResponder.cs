using Discord;
using Discord.WebSocket;
using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Outbound.DiscordUpdates
{
	public class DiscordBotResponder(DiscordSocketClient client) : IDiscordBotResponder
	{
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
	}
}
