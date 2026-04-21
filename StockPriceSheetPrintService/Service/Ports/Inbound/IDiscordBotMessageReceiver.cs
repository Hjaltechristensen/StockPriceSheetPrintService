using Discord;
using Discord.WebSocket;

namespace StockPriceSheetPrintService.Service.Ports.Inbound
{
	public interface IDiscordBotMessageReceiver
	{
		Task<string> DispatchMessageAsync(SocketMessage message, CancellationToken ct);
		Task<Modal?> DispatchMessageComponentAsync(SocketMessageComponent component, CancellationToken ct);
		Task<string> DispatchModalAsync(SocketModal modal, CancellationToken ct);
	}
}
