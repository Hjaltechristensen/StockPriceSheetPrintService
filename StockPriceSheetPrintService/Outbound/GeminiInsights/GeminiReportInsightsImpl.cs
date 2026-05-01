using Google.GenAI;
using Google.GenAI.Types;
using StockPriceSheetPrintService.Service.Models.Saxo.InstrumentDetails;
using StockPriceSheetPrintService.Service.Models.Saxo.Transactions;
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
			List<SaxoTransaction> newTransfers,
			List<string> nordnetTickers,
			List<SaxoInstrument> saxoPositions,
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
					$"  - {p.Symbol} ({p.Description}), {p.AssetType}, børs: {p.Exchange?.ExchangeId ?? "ukendt"}, valuta: {p.CurrencyCode}"))
				: "  Ingen positioner fundet";

			var nordnetTickersText = nordnetTickers.Count > 0
				? string.Join(", ", nordnetTickers)
				: "ingen";

			var userPrompt =
				$"Porteføljedata for i dag:\n\n" +
				$"Porteføljeværdier:\n" +
				$"  Saxo: {saxoBalance:N2} DKK\n" +
				$"  Nordnet: {nordnetValue:N2} DKK\n" +
				$"  June (Danske Invest): {juneValue:N2} DKK\n" +
				$"  Total: {total:N2} DKK\n" +
				$"  Ændring siden i går: {sign}{change:N2} DKK ({sign}{changePct}%)\n\n" +
				$"Saxo-beholdning:\n{saxoPositionsText}\n\n" +
				$"Nordnet-tickers: {nordnetTickersText}\n\n" +
				$"{transfersText}\n\n" +
				$"Søg efter dagens kursbevægelser for disse instrumenter og forklar kort hvad der drev porteføljeudviklingen.";

			string basePrompt =
			"Du er en kortfattet porteføljeassistent. Du modtager dagens porteføljedata inkl. aktuelle beholdninger fra Saxo Bank og Nordnet. " +
			"Brug web search til at finde dagens kursbevægelser og nyheder for de specifikke instrumenter i beholdningen. " +
			"Giv derefter en skarp kommentar (max 3-4 sætninger) der konkret forklarer hvad der drev op- eller nedturen. " +
			"Nævn specifikke instrumenter og årsager. Svar på dansk. Undgå generiske fraser." +
			$"Her kommer dagens tal: {userPrompt}";


			try
			{
				var response = await _client.Models.GenerateContentAsync(
					model: _model,
					contents: basePrompt,
					config: _config);

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
