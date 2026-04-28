using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using StockPriceSheetPrintService.Service.Models.Saxo.InstrumentDetails;
using StockPriceSheetPrintService.Service.Models.Saxo.Transactions;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Outbound.ClaudeInsights
{
	public class ClaudeReportInsightsImpl(IConfiguration configuration, ILogger<ClaudeReportInsightsImpl> logger) : IClaudeReportInsights
	{
		private const string _model = "claude-haiku-4-5-20251001";
		private const int MaxTokens = 512;

		private readonly AnthropicClient _client = new()
		{
			ApiKey = configuration["Claude:ApiKey"] ?? throw new InvalidOperationException("Claude:ApiKey missing")
		};

		private const string SystemPrompt =
			"Du er en kortfattet porteføljeassistent. Du modtager dagens porteføljedata inkl. aktuelle beholdninger fra Saxo Bank og Nordnet. " +
			"Brug web search til at finde dagens kursbevægelser og nyheder for de specifikke instrumenter i beholdningen. " +
			"Giv derefter en skarp kommentar (max 3-4 sætninger) der konkret forklarer hvad der drev op- eller nedturen. " +
			"Nævn specifikke instrumenter og årsager. Svar på dansk. Undgå generiske fraser.";

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
				? $"Nye overførsler: {string.Join(", ", newTransfers.Select(t => $"{t.BookedAmount:N2} DKK"))}"
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

			var parameters = new MessageCreateParams
			{
				MaxTokens = MaxTokens,
				System = new List<TextBlockParam>
				{
					new() { Text = SystemPrompt, CacheControl = new CacheControlEphemeral() }
				},
				Tools = [new ToolUnion(new WebSearchTool20260209())],
				Messages =
				[
					new() { Role = Role.User, Content = userPrompt }
				],
				Model = _model,
			};

			try
			{
				var response = await _client.Messages.Create(parameters, ct);
				response.Validate();
				
				const decimal inputPricePerMillion = 1.00m;
				const decimal outputPricePerMillion = 5.00m;

				var inputCostUsd = (response.Usage.InputTokens / 1_000_000m) * inputPricePerMillion;
				var outputCostUsd = (response.Usage.OutputTokens / 1_000_000m) * outputPricePerMillion;
				var totalCostUsd = inputCostUsd + outputCostUsd;

				logger.LogInformation(
					"[CLAUDE] Insights received. Tokens: input={InputTokens}, output={OutputTokens}, cacheRead={CacheRead} | Est. cost: ${Cost:F5} USD",
					response.Usage.InputTokens, response.Usage.OutputTokens, response.Usage.CacheReadInputTokens, totalCostUsd);


				var textParts = response.Content
					.Select(block => block.TryPickText(out var t) ? t?.Text : null)
					.Where(t => !string.IsNullOrWhiteSpace(t));

				return string.Join(" ", textParts).Trim() is { Length: > 0 } result ? result : null;
			}
			catch (AnthropicUnauthorizedException ex)
			{
				logger.LogError(ex, "[CLAUDE] Unauthorized - check API key");
				return null;
			}
			catch (AnthropicRateLimitException ex)
			{
				logger.LogError(ex, "[CLAUDE] Rate limited");
				return null;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "[CLAUDE] Unexpected error calling Anthropic API");
				return null;
			}
		}
	}
}
