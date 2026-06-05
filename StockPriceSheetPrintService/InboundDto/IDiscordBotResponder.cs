namespace StockPriceSheetPrintService.InboundDto
{
	public interface IDiscordBotResponder
	{
		Task SendTextAsync(ulong channelId, string text, CancellationToken ct = default);
		Task SendMenuAsync(ulong channelId, MenuBotResponse response, ulong userMessageId, CancellationToken ct = default);
		Task SendHelpAsync(ulong channelId, string text, ulong userMessageId, CancellationToken ct = default);
		Task SendUpdateAsync(ulong channelId, UpdateBotResponse response, ulong userMessageId, CancellationToken ct = default);
		Task SendGetAsync(ulong channelId, GetBotResponse response, ulong userMessageId, CancellationToken ct = default);
	}
}
