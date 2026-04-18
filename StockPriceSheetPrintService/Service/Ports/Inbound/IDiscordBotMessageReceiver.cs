using Discord.WebSocket;

namespace StockPriceSheetPrintService.Service.Ports.Inbound
{
	public interface IDiscordBotMessageReceiver
	{
		Task<string> DispatchMessageAsync(SocketMessage message, CancellationToken ct);
	}
}
