namespace StockPriceSheetPrintService.Inbound.Dto
{
	public record BotMessageCommandDto(string Command, string[] Args, ulong ChannelId);
	public record BotComponentCommandDto(string CustomId);
	public record BotModalCommandDto(string ModalId, Dictionary<string, string> Fields);
}
