using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IDiscordBotResponder
	{
		Task SendTextAsync(ulong channelId, string text, CancellationToken ct = default);
		Task SendComponentsAsync(ulong channelId, ComponentsBotResponse response, CancellationToken ct = default);
		Task SendHelpAsync(ulong channelId, string text, ulong userMessageId, CancellationToken ct = default);
	}
}
