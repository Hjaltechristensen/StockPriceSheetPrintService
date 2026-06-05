namespace StockPriceSheetPrintService.Service.Models
{
	public record BotMessageCommand(string Command, string[] Args);
	public record BotComponentCommand(string CustomId);
	public record BotModalCommand(string ModalId, Dictionary<string, string> Fields);
}
