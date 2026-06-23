using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Inbound
{
	public interface IDiscordBotMessageReceiver
	{
		Task<BotResponse> HandleMessageAsync(BotMessageCommand command, ClientContext ctx, CancellationToken ct);
		Task<BotResponse> HandleComponentAsync(BotComponentCommand command, ClientContext ctx, CancellationToken ct);
		Task<BotResponse> HandleModalAsync(BotModalCommand command, ClientContext ctx, CancellationToken ct);
	}
}
