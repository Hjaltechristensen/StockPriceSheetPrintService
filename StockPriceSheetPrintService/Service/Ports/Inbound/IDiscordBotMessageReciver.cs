using Discord.WebSocket;

namespace StockPriceSheetPrintService.Service.Ports.Inbound
{
	public interface IDiscordBotMessageReciver
	{
		Task<string> DispatchMessageAsync(SocketMessage message);
	}
}
