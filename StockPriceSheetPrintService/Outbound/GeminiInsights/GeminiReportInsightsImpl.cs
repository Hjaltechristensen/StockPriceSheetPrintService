using Google.GenAI;
using Google.GenAI.Types;
using StockPriceSheetPrintService.Service;
using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Outbound.GeminiInsights
{
	public class GeminiReportInsightsImpl(IConfiguration configuration, ILogger<GeminiReportInsightsImpl> logger) : IGeminiReportInsights
	{
		private readonly ILogger<GeminiReportInsightsImpl> _logger = logger;
		private readonly Client _client = new(apiKey: configuration["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini:ApiKey missing"));
		private readonly string _model = "gemini-2.5-flash-lite";
		private readonly GenerateContentConfig _config = new()
		{
			Tools =
			[
				new Tool { GoogleSearch = new GoogleSearch() }
			]
		};

		public async Task<string?> GetInsightsAsync(
			decimal saxoBalance,
			decimal nordnetValue,
			decimal juneValue,
			decimal total,
			decimal previousDayValue,
			List<Transfer> newTransfers,
			List<string> nordnetTickers,
			List<Instrument> saxoPositions,
			ClientContext ctx,
			CancellationToken ct)
		{
			var change = total - previousDayValue;
			var changePct = previousDayValue != 0 ? Math.Round((change / previousDayValue) * 100, 2) : 0;
			var sign = change >= 0 ? "+" : "";

			var transfersText = newTransfers.Count > 0
				? $"Nye overførsler: {string.Join(", ", newTransfers.Select(t => $"{t.Amount:N2} DKK"))}"
				: "Ingen nye overførsler";

			var saxoPositionsText = saxoPositions.Count > 0
				? string.Join("\n", saxoPositions.Select(p =>
					$"  - {p.Symbol} ({p.Description}), {p.AssetType}, børs: {p.ExchangeId ?? "ukendt"}, valuta: {p.Currency}"))
				: "  Ingen positioner fundet";

			var nordnetTickersText = nordnetTickers.Count > 0
				? string.Join(", ", nordnetTickers)
				: "ingen";

			var yesterdaysDate = DateTime.Now.AddDays(-1).ToString("dd-MM-yyyy");

var userPrompt =
    $"Dagens dato: {yesterdaysDate}\n\n" +
    $"Porteføljeværdier:\n" +
    $"  Saxo: {saxoBalance:N2} DKK\n" +
    $"  Nordnet: {nordnetValue:N2} DKK\n" +
    $"  June (Danske Invest): {juneValue:N2} DKK\n" +
    $"  Total: {total:N2} DKK\n" +
    $"  Ændring siden i går: {sign}{change:N2} DKK ({sign}{changePct}%)\n\n" +
    $"Saxo-beholdning:\n{saxoPositionsText}\n\n" +
    $"Nordnet-tickers: {nordnetTickersText}\n\n" +
    $"{transfersText}";

			string basePrompt =
    "Du er en finansiel analytiker. Din opgave er at forklare dagens bevægelser i en portefølje.\n\n" +
    "PROCES:\n" +
    $"1. Søg efter de specifikke lukkekurser eller dagsafkast for hver ticker for datoen {yesterdaysDate}.\n" +
    "2. Identificer de 3 største positive og negative bidragsydere.\n" +
    "3. Dobbelttjek at din forklaring matcher de faktiske kursdata (hvis en aktie er faldet, må du IKKE skrive den trak op).\n\n" +
    "REGLER:\n" +
    "- Vær ekstremt præcis med retningen (op/ned).\n" +
    "- Hvis du ikke kan finde data for i dag, så skriv 'Data ikke fundet for [ticker]'.\n" +
    "- Max 3-4 skarpe sætninger.\n" +
    "- Svar på dansk.\n\n" +
    $"Her er porteføljedata: {userPrompt}";


			try
			{
				var response = await _client.Models.GenerateContentAsync(
					model: _model,
					contents: basePrompt,
					config: _config,
					cancellationToken: ct);

				const decimal inputPricePerMillion = 0.075m;
				const decimal outputPricePerMillion = 0.30m;

				var usage = response?.UsageMetadata;

				if (usage != null)
				{
					var totalInputTokens = (usage.PromptTokenCount ?? 0) + (usage.ToolUsePromptTokenCount ?? 0);
					var totalOutputTokens = usage.CandidatesTokenCount ?? 0;

					var inputCostUsd = (totalInputTokens / 1_000_000m) * inputPricePerMillion;
					var outputCostUsd = (totalOutputTokens / 1_000_000m) * outputPricePerMillion;
					var totalCostUsd = inputCostUsd + outputCostUsd;

					_logger.LogInformation(
						"[GEMINI] Insights received. Tokens: input={InputTokens}, output={OutputTokens}, toolUse={ToolTokens} | Est. cost: ${Cost:F5} USD",
						totalInputTokens,
						totalOutputTokens,
						response?.UsageMetadata?.ToolUsePromptTokenCount ?? 0,
						totalCostUsd);
				}

				return response?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
			}
			catch (ClientError ex) when (ex.Message.Contains("429"))
			{
				_logger.LogError(ex, "Kvote opbrugt! Vi må vente til i morgen.");
				return null;
			}
			catch (ClientError ex)
			{
				_logger.LogError(ex, "Konfigurationsfejl: {Msg}", ex.Message);
				return null;
			}
			catch (ServerError ex)
			{
				_logger.LogError(ex, "Google har tekniske problemer (5xx). Prøv igen senere.");
				return null;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Uventet fejl: {Msg}", ex.Message);
				return null;
			}
		}
	}
}
