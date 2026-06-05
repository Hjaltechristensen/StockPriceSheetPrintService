using Discord.WebSocket;
using StockPriceSheetPrintService.Inbound.Dto;
using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Inbound.Mappers
{
	public static class BotMapper
	{
		public static BotMessageCommandDto ToCommandDto(SocketMessage msg)
		{
			var parts = msg.Content.Split(' ');
			return new BotMessageCommandDto(parts[0], parts[1..], msg.Channel.Id);
		}

		public static BotComponentCommandDto ToComponentDto(SocketMessageComponent interaction) =>
			new(interaction.Data.CustomId);

		public static BotModalCommandDto ToModalDto(SocketModal modal) =>
			new(modal.Data.CustomId, modal.Data.Components.ToDictionary(c => c.CustomId, c => c.Value));

		public static BotMessageCommand ToDomain(BotMessageCommandDto dto) =>
			new(dto.Command, dto.Args);

		public static BotComponentCommand ToDomain(BotComponentCommandDto dto) =>
			new(dto.CustomId);

		public static BotModalCommand ToDomain(BotModalCommandDto dto) =>
			new(dto.ModalId, dto.Fields);
	}
}
