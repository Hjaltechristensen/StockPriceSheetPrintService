using StockPriceSheetPrintService.Service.Exceptions;
using StockPriceSheetPrintService.Service.Models;
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
		ISchedulerStatus schedulerStatus,
		IGeminiToggle geminiToggle) : IDiscordBotMessageReceiver
	{
		public async Task<BotResponse> HandleMessageAsync(BotMessageCommand command, CancellationToken ct) =>
			command.Command switch
			{
				"!refreshToken" => await HandleRefreshToken(ct),
				"!trigger"      => await HandleTrigger(ct),
				"!start"        => HandleStartMenu(),
				"!help"         => HandleHelp(),
				_               => new EmptyBotResponse()
			};

		public async Task<BotResponse> HandleComponentAsync(BotComponentCommand command, CancellationToken ct)
		{
			return command.CustomId switch
			{
				"btn_back"           => HandleStartMenu(),
				"btn_get"            => HandleGetButtons(),
				"btn_update"         => HandleUpdateButtons(),
				"btn_actions"        => HandleActionsButtons(),
				"btn_trigger"        => AsEphemeral(await HandleTrigger(ct)),
				"btn_refreshToken"   => AsEphemeral(await HandleRefreshToken(ct)),
				"btn_help"           => HandleHelp(),
				"btn_toggle_gemini"  => HandleToggleGemini(),
				"btn_june" => new ModalBotResponse("Choose number", "june_modal", [
					new BotModalField("New June share amount", "input_share_count", "June share amount...")
				]),
				"btn_nordnet_cash" => new ModalBotResponse("Update Nordnet cash", "nordnet_cash_modal", [
					new BotModalField("New Nordnet cash amount", "input_cash_amount", "Nordnet cash amount...")
				]),
				"btn_nordnet_add" => new ModalBotResponse("Add Nordnet ticker", "nordnet_add_modal", [
					new BotModalField("Ticker", "input_ticker", "Ticker symbol, e.g. 2B76.DE"),
					new BotModalField("Amount", "input_amount", "Amount of shares, e.g. 218")
				]),
				"btn_nordnet_remove" => new ModalBotResponse("Remove Nordnet ticker", "nordnet_remove_modal", [
					new BotModalField("Ticker", "input_ticker", "Ticker symbol, e.g. 2B76.DE")
				]),
				"btn_get_cash"    => AsEphemeral(await HandleGetNordnetCash()),
				"btn_get_june"    => AsEphemeral(await HandleGetJuneSharesAmount()),
				"btn_get_symbols" => AsEphemeral(await HandleGetNordnetSymbols()),
				"btn_get_status"  => AsEphemeral(HandleStatus()),
				_ => new EmptyBotResponse()
			};
		}

		private static BotResponse AsEphemeral(BotResponse response) =>
			response is TextBotResponse t ? t with { Ephemeral = true } : response;

		public async Task<BotResponse> HandleModalAsync(BotModalCommand command, CancellationToken ct) =>
			command.ModalId switch
			{
				"june_modal"          => await HandleJuneModal(command.Fields),
				"nordnet_cash_modal"  => await HandleNordnetCashModal(command.Fields),
				"nordnet_add_modal"   => await HandleNordnetAddModal(command.Fields),
				"nordnet_remove_modal"=> await HandleNordnetRemoveModal(command.Fields),
				_                     => new EmptyBotResponse()
			};

		private static MenuBotResponse HandleStartMenu() =>
			new("📋 **Portfolio menu:**", [
				new BotButton("📊 Get values",   "btn_get",     BotButtonStyle.Primary),
				new BotButton("✏️ Update values", "btn_update",  BotButtonStyle.Primary),
				new BotButton("⚙️ Actions",       "btn_actions", BotButtonStyle.Primary)
			]);

		private MenuBotResponse HandleActionsButtons() =>
			new("⚙️ **Actions:**", [
				new BotButton("⚡ Trigger portfolio",                   "btn_trigger",       BotButtonStyle.Action),
				new BotButton("🔑 Refresh token",                       "btn_refreshToken",  BotButtonStyle.Action),
				new BotButton(geminiToggle.IsEnabled ? "🤖 Gemini: TIL" : "🤖 Gemini: FRA",
							  "btn_toggle_gemini",
							  geminiToggle.IsEnabled ? BotButtonStyle.Action : BotButtonStyle.Action),
				new BotButton("❓ Help",                                 "btn_help",          BotButtonStyle.Secondary),
				new BotButton("⬅️ Back",                                "btn_back",          BotButtonStyle.Secondary)
			]);

		private BotResponse HandleToggleGemini()
		{
			geminiToggle.Toggle();
			return new TextBotResponse(
				geminiToggle.IsEnabled ? "🤖 Gemini insights: **slået TIL**" : "🤖 Gemini insights: **slået FRA**",
				Ephemeral: true);
		}

		private static GetBotResponse HandleGetButtons() =>
			new("Get values:", [
				new BotButton("Nordnet cash",    "btn_get_cash",    BotButtonStyle.Action),
				new BotButton("June shares",     "btn_get_june",    BotButtonStyle.Action),
				new BotButton("Nordnet symbols", "btn_get_symbols", BotButtonStyle.Action),
				new BotButton("Status",          "btn_get_status",  BotButtonStyle.Action),
				new BotButton("⬅️ Back",         "btn_back",        BotButtonStyle.Secondary)
			]);

		private static UpdateBotResponse HandleUpdateButtons() =>
			new("Update values:", [
				new BotButton("June share count",      "btn_june",           BotButtonStyle.Action),
				new BotButton("Nordnet cash",          "btn_nordnet_cash",   BotButtonStyle.Action),
				new BotButton("Add Nordnet ticker",    "btn_nordnet_add",    BotButtonStyle.Action),
				new BotButton("Remove Nordnet ticker", "btn_nordnet_remove", BotButtonStyle.Action),
				new BotButton("⬅️ Back",               "btn_back",           BotButtonStyle.Secondary)
			]);

		private static BotResponse HandleHelp() => new HelpBotResponse("""
			📋 **Kommandoer**
			`!start` — Vis hoved-menu med knapper
			`!trigger` — Kør portfolio-job manuelt
			`!refreshToken` — Opdatér Saxo access token manuelt
			`!help` — Vis denne besked
			""");

		private BotResponse HandleStatus()
		{
			string Fmt(DateTimeOffset? t) => t is { } v ? $"<t:{v.ToUnixTimeSeconds()}:R>" : "Ukendt";
			string FmtAbsolute(DateTimeOffset? t) => t is { } v ? $"{v:dd/MM/yyyy HH:mm} UTC" : "Aldrig";
			var lastRunStatus = schedulerStatus.LastRunSucceeded switch
			{
				true  => "✅ Success",
				false => "❌ Fejl",
				null  => "Ukendt"
			};
			return new TextBotResponse($"""
				📊 **Service Status**
				⏳ **Næste job run:** {Fmt(schedulerStatus.NextRunAt)} ({FmtAbsolute(schedulerStatus.NextRunAt)})
				🔑 **Næste token refresh:** {(schedulerStatus.NextTokenRefreshAt is not null ? Fmt(schedulerStatus.NextTokenRefreshAt) : "Ingen planlagt")}
				🕐 **Sidste run:** {FmtAbsolute(schedulerStatus.LastRunAt)}
				📋 **Sidste run status:** {lastRunStatus}
				🤖 **Gemini insights:** {(geminiToggle.IsEnabled ? "TIL" : "FRA")}
				""");
		}

		private async Task<BotResponse> HandleRefreshToken(CancellationToken ct)
		{
			await using var scope = scopeFactory.CreateAsyncScope();
			var saxoTokenService = scope.ServiceProvider.GetRequiredService<ISaxoTokenService>();
			var accessToken = await saxoTokenService.GetAccessTokenAsync(ct);
			return new TextBotResponse(accessToken != null
				? "✅ AccessToken successfully updated"
				: "❌ Failed to update AccessToken");
		}

		private async Task<BotResponse> HandleTrigger(CancellationToken ct)
		{
			try
			{
				await using var scope = scopeFactory.CreateAsyncScope();
				var jobRunner = scope.ServiceProvider.GetRequiredService<IPortfolioJobRunner>();
				await jobRunner.RunJobAsync(ct, true);
				return new TextBotResponse("✅ Portfolio run triggered successfully");
			}
			catch (Exception ex)
			{
				return new TextBotResponse($"❌ Error triggering job: {ex.Message}");
			}
		}

		private async Task<BotResponse> HandleGetNordnetCash()
		{
			try
			{
				var result = await nordnetStore.GetNordnetCashAmountAsync();
				return new TextBotResponse($"💰 Cash: {result.CashAmount:N2} DKK (Last updated: {result.LastUpdated:dd/MM/yyyy HH:mm})");
			}
			catch (NordnetStoreException ex)
			{
				return new TextBotResponse($"❌ Error getting cash: {ex.Message}");
			}
		}

		private async Task<BotResponse> HandleGetJuneSharesAmount()
		{
			try
			{
				var result = await juneStore.GetJuneSharesAmountAsync();
				return new TextBotResponse($"📊 June shares: {result.Amount:N4} stk. (Last updated: {result.LastUpdated:dd/MM/yyyy HH:mm})");
			}
			catch (JuneStoreException ex)
			{
				return new TextBotResponse($"❌ Error getting June shares: {ex.Message}");
			}
		}

		private async Task<BotResponse> HandleGetNordnetSymbols()
		{
			try
			{
				var symbols = await nordnetSymbolStore.GetSymbolsAsync();
				var lines = symbols.Select(kvp => $"`{kvp.Key}` — {kvp.Value:N0} stk.");
				return new TextBotResponse($"📈 **Nordnet symbols:**\n{string.Join('\n', lines)}");
			}
			catch (NordnetSymbolStoreException ex)
			{
				return new TextBotResponse($"❌ Error fetching symbols: {ex.Message}");
			}
		}

		private async Task<BotResponse> HandleJuneModal(Dictionary<string, string> fields)
		{
			var normalized = fields.GetValueOrDefault("input_share_count")?.Replace(',', '.');
			if (!decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
				return new TextBotResponse("❌ Invalid format. Use only numbers e.g. 710", Ephemeral: true);
			try
			{
				await juneStore.SetJuneSharesAmountAsync(amount);
				return new TextBotResponse($"✅ June shares updated: {amount:N2} stk.", Ephemeral: true);
			}
			catch (JuneStoreException ex)
			{
				return new TextBotResponse($"❌ Error updating June shares: {ex.Message}", Ephemeral: true);
			}
		}

		private async Task<BotResponse> HandleNordnetCashModal(Dictionary<string, string> fields)
		{
			var normalized = fields.GetValueOrDefault("input_cash_amount")?.Replace(',', '.');
			if (!decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
				return new TextBotResponse("❌ Invalid format. Use only numbers e.g. 1000.50", Ephemeral: true);
			try
			{
				await nordnetStore.SetNordnetCashAmountAsync(amount);
				return new TextBotResponse($"✅ Cash amount updated: {amount:N2} DKK", Ephemeral: true);
			}
			catch (NordnetStoreException ex)
			{
				return new TextBotResponse($"❌ Error updating cash: {ex.Message}", Ephemeral: true);
			}
		}

		private async Task<BotResponse> HandleNordnetAddModal(Dictionary<string, string> fields)
		{
			var ticker = fields.GetValueOrDefault("input_ticker")?.ToUpperInvariant();
			var normalized = fields.GetValueOrDefault("input_amount")?.Replace(',', '.');
			if (!decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var shares) || string.IsNullOrEmpty(ticker))
				return new TextBotResponse("❌ Invalid format for amount or ticker.", Ephemeral: true);
			try
			{
				await nordnetSymbolStore.AddOrUpdateSymbolAsync(ticker, shares);
				return new TextBotResponse($"✅ Symbol updated: {ticker} = {shares:N0} stk.", Ephemeral: true);
			}
			catch (NordnetSymbolStoreException ex)
			{
				return new TextBotResponse($"❌ Error updating symbol: {ex.Message}", Ephemeral: true);
			}
		}

		private async Task<BotResponse> HandleNordnetRemoveModal(Dictionary<string, string> fields)
		{
			var ticker = fields.GetValueOrDefault("input_ticker")?.ToUpperInvariant();
			if (string.IsNullOrEmpty(ticker))
				return new TextBotResponse("❌ Invalid format for ticker.", Ephemeral: true);
			try
			{
				await nordnetSymbolStore.RemoveSymbolAsync(ticker);
				return new TextBotResponse($"✅ Symbol removed: {ticker}", Ephemeral: true);
			}
			catch (NordnetSymbolStoreException ex)
			{
				return new TextBotResponse($"❌ Error removing symbol: {ex.Message}", Ephemeral: true);
			}
		}
	}
}
