using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Inbound
{
	public interface IDiscordBotMessageReceiver
	{
		Task<BotResponse> HandleMessageAsync(BotMessageCommand command, CancellationToken ct);
		Task<BotResponse> HandleComponentAsync(BotComponentCommand command, CancellationToken ct);
		Task<BotResponse> HandleModalAsync(BotModalCommand command, CancellationToken ct);
	}
}
