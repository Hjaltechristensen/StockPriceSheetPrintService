using StockPriceSheetPrintService.InboundDto;

namespace StockPriceSheetPrintService.InboundMappers
{
	public static class DiscordToInboundMapper
	{
		public static BotMessageCommand FromMessage(string content, ulong channelId)
		{
			var parts = content.Split(' ');
			return new BotMessageCommand(parts[0], parts[1..], channelId);
		}

		public static BotComponentCommand FromButton(string customId) =>
			new(customId);

		public static BotModalCommand FromModal(string modalId, IEnumerable<(string CustomId, string Value)> components) =>
			new(modalId, components.ToDictionary(c => c.CustomId, c => c.Value));
	}
}
