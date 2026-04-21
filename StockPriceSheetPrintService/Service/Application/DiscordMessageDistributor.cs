using Discord;
using Discord.WebSocket;
using Microsoft.VisualBasic;
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
		INordnetSymbolStore nordnetSymbolStore,
		ISchedulerStatus schedulerStatus) : IDiscordBotMessageReceiver
	{
		private readonly INordnetStore _nordnetStore = nordnetStore;
		private readonly IJuneStore _juneStore = juneStore;
		private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
		private readonly INordnetSymbolStore _nordnetSymbolStore = nordnetSymbolStore;
		private readonly ISchedulerStatus _schedulerStatus = schedulerStatus;
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

				case "!update":
					return await UpdateValues(message);

				default:
					return string.Empty;
			}
		}

		public Task<Modal?> DispatchMessageComponentAsync(SocketMessageComponent component, CancellationToken ct)
		{
			switch (component.Data.CustomId)
			{
				case "btn_june":
					return Task.FromResult<Modal?>(new ModalBuilder()
						.WithTitle("Choose number")
						.WithCustomId("june_modal")
						.AddTextInput("New June share amount", customId: "input_share_count", placeholder: "June share amount...")
						.Build());

				case "btn_nordnet_cash":
					return Task.FromResult<Modal?>(new ModalBuilder()
						.WithTitle("Update Nordnet cash")
						.WithCustomId("nordnet_cash_modal")
						.AddTextInput("New Nordnet cash amount", customId: "input_cash_amount", placeholder: "Nordnet cash amount...")
						.Build());

				case "btn_nordnet_add":
					return Task.FromResult<Modal?>(new ModalBuilder()
						.WithTitle("Add Nordnet ticker")
						.WithCustomId("nordnet_add_modal")
						.AddTextInput("Ticker", customId: "input_ticker", placeholder: "Ticker symbol, e.g. 2B76.DE")
						.AddTextInput("Amount", customId: "input_amount", placeholder: "Amount of shares, e.g. 218")
						.Build());

				case "btn_nordnet_remove":
					return Task.FromResult<Modal?>(new ModalBuilder()
						.WithTitle("Remove Nordnet ticker")
						.WithCustomId("nordnet_remove_modal")
						.AddTextInput("Ticker", customId: "input_ticker", placeholder: "Ticker symbol, e.g. 2B76.DE")
						.Build());
				default:
					return Task.FromResult<Modal?>(null);
			}
		}

		public async Task<string> DispatchModalAsync(SocketModal modal, CancellationToken ct)
		{
			switch (modal.Data.CustomId)
			{
				case "june_modal":
					var shareCount = modal.Data.Components.FirstOrDefault(c => c.CustomId == "input_share_count")?.Value;
					if (!int.TryParse(shareCount, NumberStyles.Any, CultureInfo.InvariantCulture, out int shareCountInt))
						return "❌ Invalid format. Use only numbers e.g. 710";
					
					try
					{
						await _juneStore.SetJuneSharesAmountAsync(shareCountInt);
						return $"✅ June shares updated: {shareCountInt:N2} stk.";
					}
					catch (JuneStoreException ex)
					{
						return $"❌ Error updating June shares: {ex.Message}";
					}

				case "nordnet_cash_modal":
					var cashAmount = modal.Data.Components.FirstOrDefault(c => c.CustomId == "input_cash_amount")?.Value;
					if (!int.TryParse(cashAmount, NumberStyles.Any, CultureInfo.InvariantCulture, out int cashAmountInt))
						return "❌ Invalid format. Use only numbers e.g. 1000.50";
					
					try
					{
						await _nordnetStore.SetNordnetCashAmountAsync(cashAmountInt);
						return $"✅ Cash amount updated: {cashAmountInt:N2} DKK";
					}
					catch (NordnetStoreException ex)
					{
						return $"❌ Error updating cash: {ex.Message}";
					}

				case "nordnet_add_modal":
					var ticker = modal.Data.Components.FirstOrDefault(c => c.CustomId == "input_ticker")?.Value.ToUpperInvariant();
					var amount = modal.Data.Components.FirstOrDefault(c => c.CustomId == "input_amount")?.Value;
					if (!int.TryParse(amount, NumberStyles.Any, CultureInfo.InvariantCulture, out int shares) || string.IsNullOrEmpty(ticker))
						return "❌ Invalid format for amount or ticker. Use only numbers e.g. 218 and valid ticker symbol.";

					try
					{
						await _nordnetSymbolStore.AddOrUpdateSymbolAsync(ticker, shares);
						return $"✅ Symbol updated: {ticker} = {shares:N0} stk.";
					}
					catch (NordnetSymbolStoreException ex)
					{
						return $"❌ Error updating symbol: {ex.Message}";
					}

				case "nordnet_remove_modal":
					var tickerToRemove = modal.Data.Components.FirstOrDefault(c => c.CustomId == "input_ticker")?.Value.ToUpperInvariant();
					if (string.IsNullOrEmpty(tickerToRemove))
						return "❌ Invalid format for ticker. Use valid ticker symbol.";

					try
					{
						await _nordnetSymbolStore.RemoveSymbolAsync(tickerToRemove);
						return $"✅ Symbol removed: {tickerToRemove}";
					}
					catch (NordnetSymbolStoreException ex)
					{
						return $"❌ Error removing symbol: {ex.Message}";
					}
				default:
					return string.Empty;
			}
		}

		private static string HandleHelp()
		{
			return """
        📋 **Saxo**
        `!refreshToken` — Manually refresh Saxo access token

        💰 **Nordnet**
        `!getSymbols` — Show all active symbols
        `!getCash` — Show current Nordnet cash balance
        `!updateCash <balance>` — Update cash balance, e.g. `!updateCash 1000.50`
        `!addSymbol <ticker> <amount>` — Add/update symbol, e.g. `!addSymbol 2B76.DE 218`
        `!removeSymbol <ticker>` — Remove symbol, e.g. `!removeSymbol O`

        📊 **June**
        `!updateJune <amount>` — Update June share count, e.g. `!updateJune 710`
        `!getJuneAmount` — Show current June share count

        ℹ️ **Andet**
        `!trigger` — Manually trigger the full portfolio run
        `!status` — Show last run time and portfolio value
        `!help` — Show this message
        """;
		}

		private async Task<string> UpdateValues(SocketMessage message)
		{
			var component = new ComponentBuilder()
		.WithButton("June share count", customId: "btn_june", ButtonStyle.Primary)
		.WithButton("Nordnet cash", customId: "btn_nordnet_cash", ButtonStyle.Primary)
		.WithButton("Add Nordnet ticker", customId: "btn_nordnet_add", ButtonStyle.Primary)
		.WithButton("Remove Nordnet ticker", customId: "btn_nordnet_remove", ButtonStyle.Primary)
		.Build();
			await message.Channel.SendMessageAsync("Update values:", components: component);
			return string.Empty;
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
				await _juneStore.SetJuneSharesAmountAsync(amount);
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
				var result = await _juneStore.GetJuneSharesAmountAsync();
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

		private string HandleStatus()
		{
			var s = _schedulerStatus;

			string Fmt(DateTimeOffset? t) => t is { } v
				? $"<t:{v.ToUnixTimeSeconds()}:R>"
				: "Ukendt";

			string FmtAbsolute(DateTimeOffset? t) => t is { } v
				? $"{v:dd/MM/yyyy HH:mm} UTC"
				: "Aldrig";

			var lastRunStatus = s.LastRunSucceeded switch
			{
				true => "✅ Success",
				false => "❌ Fejl",
				null => "Ukendt"
			};

			return $"""
			📊 **Service Status**
			⏳ **Næste job run:** {Fmt(s.NextRunAt)} ({FmtAbsolute(s.NextRunAt)})
			🔑 **Næste token refresh:** {(s.NextTokenRefreshAt is not null ? Fmt(s.NextTokenRefreshAt) : "Ingen planlagt")}
			🕐 **Sidste run:** {FmtAbsolute(s.LastRunAt)}
			📋 **Sidste run status:** {lastRunStatus}
			""";
		}
	}
}
