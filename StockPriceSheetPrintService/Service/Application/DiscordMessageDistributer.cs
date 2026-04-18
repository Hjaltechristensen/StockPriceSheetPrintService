using Discord.WebSocket;
using StockPriceSheetPrintService.Service.Exceptions;
using StockPriceSheetPrintService.Service.Ports.Inbound;
using StockPriceSheetPrintService.Service.Ports.Outbound;
using System.Globalization;

namespace StockPriceSheetPrintService.Service.Application
{
	public class DiscordMessageDistributer(INordnetStore nordnetStore) : IDiscordBotMessageReciver
	{
		private readonly INordnetStore _nordnetStore = nordnetStore;
		public async Task<string> DispatchMessageAsync(SocketMessage message)
		{
			switch (message.Content.Split(' ')[0])
			{
				case "!updateCash":
					return await HandleUpdateCash(message);

				case "!getCash":
					return await HandleGetCash();

				case "!help":
					return await Help();

				default:
					return string.Empty;
			}
		}

		private async Task<string> HandleUpdateCash(SocketMessage message)
		{
			var parts = message.Content.Split(' ');
			if (parts.Length != 2 || !decimal.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
				return "❌ Ugyldigt format. Brug: !updateCash 12500.50";

			try
			{
				await _nordnetStore.SetCashAmountAsync(amount);
				return $"✅ Kontanter opdateret til {amount:N2} DKK";
			}
			catch (NordnetStoreException ex)
			{
				return $"❌ Fejl ved opdatering: {ex.Message}";
			}
		}

		private async Task<string> HandleGetCash()
		{
			try
			{
				var result = await _nordnetStore.GetCashAmountAsync();
				return $"💰 Kontanter: {result.CashAmount:N2} DKK (sidst opdateret {result.LastUpdated:dd/MM/yyyy HH:mm})";
			}
			catch (NordnetStoreException ex)
			{
				return $"❌ Fejl ved hentning: {ex.Message}";
			}
		}

		private static Task<string> Help()
		{
			var help = """
				**Tilgængelige kommandoer:**
				`!getCash` – Vis nuværende kontantbeholdning hos Nordnet
				`!updateCash <beløb>` – Opdater kontantbeholdning, f.eks. `!updateCash 12500.50`
				`!help` – Vis denne besked
				""";

			return Task.FromResult(help);
		}
	}
}
