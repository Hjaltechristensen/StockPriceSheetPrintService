namespace StockPriceSheetPrintService.Service.Models
{
	public enum BotButtonStyle { Primary, Secondary, Action }

	public abstract record BotResponse;
	public record TextBotResponse(string Text, bool Ephemeral = false) : BotResponse;
	public record ModalBotResponse(string Title, string ModalId, List<BotModalField> Fields) : BotResponse;
	public record MenuBotResponse(string Text, List<BotButton> Buttons) : BotResponse;
	public record HelpBotResponse(string Text) : BotResponse;
	public record UpdateBotResponse(string Text, List<BotButton> Buttons) : BotResponse;
	public record GetBotResponse(string Text, List<BotButton> Buttons) : BotResponse;
	public record EmptyBotResponse : BotResponse;

	public record BotModalField(string Label, string CustomId, string Placeholder);
	public record BotButton(string Label, string CustomId, BotButtonStyle Style = BotButtonStyle.Primary);
}
