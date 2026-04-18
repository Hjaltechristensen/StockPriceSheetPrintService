using Discord.WebSocket;
using StockPriceSheetPrintService.Service.Exceptions;
using StockPriceSheetPrintService.Service.Ports.Inbound;
using StockPriceSheetPrintService.Service.Ports.Outbound;
using System.Globalization;

namespace StockPriceSheetPrintService.Service.Application
{
	public class DiscordMessageDistributor(
		INordnetStore nordnetStore, 
		ISaxoTokenService saxoTokenService,
		IPortfolioJobRunner portfolioJobRunner,
		IJuneStore juneStore) : IDiscordBotMessageReceiver
	{
		private readonly INordnetStore _nordnetStore = nordnetStore;
		private readonly ISaxoTokenService _saxoTokenService = saxoTokenService;
		private readonly IPortfolioJobRunner _portfolioJobRunner = portfolioJobRunner;
		private readonly IJuneStore _juneStore = juneStore;
		public async Task<string> DispatchMessageAsync(SocketMessage message, CancellationToken ct)
		{
			switch (message.Content.Split(' ')[0])
			{
				case "!refreshToken":
					return await HandleRefreshToken(ct);

				case "!trigger":
					return await HandleManuelTrigger(ct);

				case "!updateCash":
					return await HandleUpdateNordnetCash(message);

				case "!updateShares":
					return await HandleUpdateNordnetShares(message);

				case "!updateJune":
					return await HandleUpdateJuneSharesAmount(message);

				case "!help":
					return await HandleHelp();

				case "!status":
					return await HandleStatus();

				case "!getCash":
					return await HandleGetNordnetCash();

				case "!getJuneAmount":
					return await HandleGetJuneSharesAmount();

				default:
					return string.Empty;
			}
		}

		private static Task<string> HandleHelp()
		{
			var help = """
				**Available commands:**
				`!refreshToken`						Manuelly update refreshToken for Saxo
				`!trigger`							Manuelly trigger whole flow
				`!updateCash <cash-balance>`		Update cash balance at Nordnet, e.g. `!updateCash 1000.50`
				`!updateShares <ticker> <amount>`	Update Nordnet shares, e.g. `!updateShares 2B76.DE 20`
				`!updateJune <amount>`				Update amount of June shares, e.g. `!updateJune 210`
				`!getJuneAmount`					Show amount of june shares
				`!status`							Show when job was last run and the lastest portfolio value from Google Sheet			
				`!getCash`							Show current cash balance at Nordnet
				`!help`								Show this message
				""";

			return Task.FromResult(help);
		}

		private async Task<string> HandleUpdateNordnetCash(SocketMessage message)
		{
			var parts = message.Content.Split(' ');
			if (parts.Length != 2 || !decimal.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
				return "❌ Invalid format. Use: !updateCash 1000.50";

			try
			{
				await _nordnetStore.SetNordnetCashAmountAsync(amount);
				return $"✅ Cash amount updated - Value: {amount:N2} DKK";
			}
			catch (NordnetStoreException ex)
			{
				return $"❌ Error while updating NordnetCash - Exception: {ex.Message}";
			}
		}

		private async Task<string> HandleUpdateJuneSharesAmount(SocketMessage message)
		{
			var parts = message.Content.Split(' ');
			if (parts.Length != 2 || !decimal.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
				return "❌ Invalid format. Use: !updateJune 1000.50";

			try
			{
				await _juneStore.SetJuneSharesAmount(amount);
				return $"✅ June shares amount updated - Value: {amount:N2} DKK";
			}
			catch (JuneStoreException ex)
			{
				return $"❌ Error while updating JuneSharesAmount - Exception: {ex.Message}";
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
				return $"❌ Error while getting NordnetCash - Exception: {ex.Message}";
			}
		}

		private async Task<string> HandleGetJuneSharesAmount()
		{
			try
			{
				var result = await _juneStore.GetJuneSharesAmount();
				return $"June shares amount: {result.Amount} DKK (Last updated: {result.LastUpdated})";
			}
			catch (JuneStoreException ex)
			{
				return $"❌ Error while getting JuneSharesAmount - Exception: {ex.Message}";
			}
		}

		private async Task<string> HandleRefreshToken(CancellationToken ct)
		{
			var accessToken = await _saxoTokenService.GetAccessTokenAsync(ct);
			if (accessToken == null)
			{
				return "✅ AccessToken successfully updated";
			}
			return "❌ Failed to update AccessToken";
		}

		private async Task<string> HandleManuelTrigger(CancellationToken ct)
		{
			await _portfolioJobRunner.RunJobAsync(ct, true);
			return "✅ Run manuelly triggerd successful";
		}

		private async Task<string> HandleStatus()
		{
			throw new NotImplementedException();
		}

		private async Task<string> HandleUpdateNordnetShares(SocketMessage message)
		{
			throw new NotImplementedException();
		}
	}
}
