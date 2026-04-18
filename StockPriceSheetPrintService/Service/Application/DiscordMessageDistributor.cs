using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using StockPriceSheetPrintService.Service.Exceptions;
using StockPriceSheetPrintService.Service.Ports.Inbound;
using StockPriceSheetPrintService.Service.Ports.Outbound;
using System.Globalization;

namespace StockPriceSheetPrintService.Service.Application
{
	public class DiscordMessageDistributor(
		INordnetStore nordnetStore,
		IJuneStore juneStore,
		IServiceScopeFactory scopeFactory) : IDiscordBotMessageReceiver
	{
		private readonly INordnetStore _nordnetStore = nordnetStore;
		private readonly IJuneStore _juneStore = juneStore;
		private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

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

				case "!updateShares":
					return await HandleUpdateNordnetShares(message);

				case "!updateJune":
					return await HandleUpdateJuneSharesAmount(message);

				case "!help":
					return HandleHelp();

				case "!status":
					return "⚠️ Not yet implemented";

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
				**Available commands:**
				`!refreshToken`                      Manually refresh Saxo access token
				`!trigger`                           Manually trigger the full portfolio run
				`!updateCash <balance>`              Update Nordnet cash balance, e.g. `!updateCash 1000.50`
				`!updateShares <ticker> <amount>`    Update Nordnet share count, e.g. `!updateShares 2B76.DE 20`
				`!updateJune <amount>`               Update June share count, e.g. `!updateJune 710`
				`!getCash`                           Show current Nordnet cash balance
				`!getJuneAmount`                     Show current June share count
				`!help`                              Show this message
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

		private static Task<string> HandleUpdateNordnetShares(SocketMessage message)
		{
			return Task.FromResult("⚠️ Not yet implemented");
		}
	}
}
