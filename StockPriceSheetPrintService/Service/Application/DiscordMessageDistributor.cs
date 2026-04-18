using Discord.WebSocket;
using StockPriceSheetPrintService.Service.Exceptions;
using StockPriceSheetPrintService.Service.Ports.Inbound;
using StockPriceSheetPrintService.Service.Ports.Outbound;
using System.Globalization;

namespace StockPriceSheetPrintService.Service.Application
{
	public class DiscordMessageDistributor(
		INordnetStore nordnetStore,
		IJuneStore juneStore,
		IServiceScopeFactory scopeFactory,
		INordnetSymbolStore nordnetSymbolStore) : IDiscordBotMessageReceiver
	{
		private readonly INordnetStore _nordnetStore = nordnetStore;
		private readonly IJuneStore _juneStore = juneStore;
		private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
		private readonly INordnetSymbolStore _nordnetSymbolStore = nordnetSymbolStore;
		public async Task<string> DispatchMessageAsync(SocketMessage message, CancellationToken ct)
		{
			switch (message.Content.Split(' ')[0])
			{
				case "!refreshToken":
					return await HandleRefreshToken(ct);

				case "!trigger":
					return await HandleTrigger(ct);

				case "!updateCash":
					return await HandleUpdateNordnetCash(message);

				case "!addSymbol":
					return await HandleAddNordnetSymbol(message);

				case "!removeSymbol":
					return await HandleRemoveNordnetSymbol(message);

				case "!getSymbols":
					return await HandleGetNordnetSymbols();

				case "!updateJune":
					return await HandleUpdateJuneSharesAmount(message);

				case "!help":
					return HandleHelp();

				case "!status":
					return HandleStatus();

				case "!getCash":
					return await HandleGetNordnetCash();

				case "!getJuneAmount":
					return await HandleGetJuneSharesAmount();

				default:
					return string.Empty;
			}
		}

		private static string HandleHelp()
		{
			return """
        📋 **Saxo & Portfolio**
        `!refreshToken` — Manually refresh Saxo access token
        `!trigger` — Manually trigger the full portfolio run

        💰 **Nordnet**
        `!updateCash <balance>` — Update cash balance, e.g. `!updateCash 1000.50`
        `!getSymbols` — Vis alle aktive symbols
        `!getCash` — Show current Nordnet cash balance
        `!addSymbol <ticker> <antal>` — Tilføj/opdater symbol, fx `!addSymbol 2B76.DE 218`
        `!removeSymbol <ticker>` — Fjern symbol, fx `!removeSymbol O`

        📊 **June**
        `!updateJune <amount>` — Update June share count, e.g. `!updateJune 710`
        `!getJuneAmount` — Show current June share count

        ℹ️ **Andet**
        `!status` — Show last run time and portfolio value
        `!help` — Show this message
        """;
		}

		private async Task<string> HandleUpdateNordnetCash(SocketMessage message)
		{
			var parts = message.Content.Split(' ');
			if (parts.Length != 2 || !decimal.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
				return "❌ Invalid format. Use: !updateCash 1000.50";

			try
			{
				await _nordnetStore.SetNordnetCashAmountAsync(amount);
				return $"✅ Cash amount updated: {amount:N2} DKK";
			}
			catch (NordnetStoreException ex)
			{
				return $"❌ Error updating cash: {ex.Message}";
			}
		}

		private async Task<string> HandleUpdateJuneSharesAmount(SocketMessage message)
		{
			var parts = message.Content.Split(' ');
			if (parts.Length != 2 || !decimal.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
				return "❌ Invalid format. Use: !updateJune 710";

			try
			{
				await _juneStore.SetJuneSharesAmount(amount);
				return $"✅ June shares updated: {amount:N2} stk.";
			}
			catch (JuneStoreException ex)
			{
				return $"❌ Error updating June shares: {ex.Message}";
			}
		}

		private async Task<string> HandleGetNordnetCash()
		{
			try
			{
				var result = await _nordnetStore.GetNordnetCashAmountAsync();
				return $"💰 Cash: {result.CashAmount:N2} DKK (Last updated: {result.LastUpdated:dd/MM/yyyy HH:mm})";
			}
			catch (NordnetStoreException ex)
			{
				return $"❌ Error getting cash: {ex.Message}";
			}
		}

		private async Task<string> HandleGetJuneSharesAmount()
		{
			try
			{
				var result = await _juneStore.GetJuneSharesAmount();
				return $"📊 June shares: {result.Amount:N4} stk. (Last updated: {result.LastUpdated:dd/MM/yyyy HH:mm})";
			}
			catch (JuneStoreException ex)
			{
				return $"❌ Error getting June shares: {ex.Message}";
			}
		}

		private async Task<string> HandleRefreshToken(CancellationToken ct)
		{
			await using var scope = _scopeFactory.CreateAsyncScope();
			var saxoTokenService = scope.ServiceProvider.GetRequiredService<ISaxoTokenService>();
			var accessToken = await saxoTokenService.GetAccessTokenAsync(ct);
			return accessToken != null
				? "✅ AccessToken successfully updated"
				: "❌ Failed to update AccessToken";
		}

		private async Task<string> HandleTrigger(CancellationToken ct)
		{
			try
			{
				await using var scope = _scopeFactory.CreateAsyncScope();
				var jobRunner = scope.ServiceProvider.GetRequiredService<IPortfolioJobRunner>();
				await jobRunner.RunJobAsync(ct, true);
				return "✅ Portfolio run triggered successfully";
			}
			catch (Exception ex)
			{
				return $"❌ Error triggering job: {ex.Message}";
			}
		}

		private async Task<string> HandleAddNordnetSymbol(SocketMessage message)
		{
			var parts = message.Content.Split(' ');
			if (parts.Length != 3 || !decimal.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var shares))
				return "❌ Invalid format. Use: !addSymbol 2B76.DE 218";

			var ticker = parts[1].ToUpperInvariant();
			try
			{
				await _nordnetSymbolStore.AddOrUpdateSymbolAsync(ticker, shares);
				return $"✅ Symbol updated: {ticker} = {shares:N0} stk.";
			}
			catch (NordnetSymbolStoreException ex)
			{
				return $"❌ Error updating symbol: {ex.Message}";
			}
		}

		private async Task<string> HandleRemoveNordnetSymbol(SocketMessage message)
		{
			var parts = message.Content.Split(' ');
			if (parts.Length != 2)
				return "❌ Invalid format. Use: !removeSymbol 2B76.DE";

			var ticker = parts[1].ToUpperInvariant();
			try
			{
				await _nordnetSymbolStore.RemoveSymbolAsync(ticker);
				return $"✅ Symbol removed: {ticker}";
			}
			catch (NordnetSymbolStoreException ex)
			{
				return $"❌ Error removing symbol: {ex.Message}";
			}
		}

		private async Task<string> HandleGetNordnetSymbols()
		{
			try
			{
				var symbols = await _nordnetSymbolStore.GetSymbolsAsync();
				var lines = symbols.Select(kvp => $"`{kvp.Key}` — {kvp.Value:N0} stk.");
				return $"📈 **Nordnet symbols:**\n{string.Join('\n', lines)}";
			}
			catch (NordnetSymbolStoreException ex)
			{
				return $"❌ Error fetching symbols: {ex.Message}";
			}
		}

		private static string HandleStatus()
		{
			return "⚠️ Not yet implemented";
		}
	}
}
