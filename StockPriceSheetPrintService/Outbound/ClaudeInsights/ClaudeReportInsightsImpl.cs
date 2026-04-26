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
		private readonly ILogger<ClaudeReportInsightsImpl> _logger = logger;
		private readonly AnthropicClient _client = new()
		{
			ApiKey = configuration["Claude:ApiKey"] ?? throw new InvalidOperationException("Claude:ApiKey missing")
		};

		private const string SystemPrompt =
			"Du er en kortfattet porteføljeassistent. Du modtager dagens porteføljedata inkl. aktuelle beholdninger fra Saxo Bank og Nordnet. " +
			"Analyser tallene og giv en skarp kommentar (max 3-4 sætninger) om dagens udvikling — hvad kan forklare op- eller nedturen? " +
			"Nævn konkrete instrumenter fra beholdningen hvis relevant. Svar på dansk. Undgå generiske fraser som 'markederne bevægede sig'.";

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
				$"Saxo-beholdning (aktuelle positioner):\n{saxoPositionsText}\n\n" +
				$"Nordnet-tickers: {nordnetTickersText}\n\n" +
				$"{transfersText}\n\n" +
				$"Giv en kort analyse af hvad der kan forklare dagens kurs-udvikling baseret på disse instrumenter.";

			var parameters = new MessageCreateParams
			{
				MaxTokens = 512,
				System = new List<TextBlockParam>
				{
					new() { Text = SystemPrompt, CacheControl = new CacheControlEphemeral() }
				},
				Messages =
				[
					new()
					{
						Role = Role.User,
						Content = userPrompt,
					},
				],
				Model = "claude-haiku-4-5",
			};

			try
			{
				var response = await _client.Messages.Create(parameters, ct);
				response.Validate();

				_logger.LogInformation(
					"[CLAUDE] Insights received. Tokens: input={InputTokens}, output={OutputTokens}, cacheRead={CacheRead}",
					response.Usage.InputTokens, response.Usage.OutputTokens, response.Usage.CacheReadInputTokens);

				if (response.Content.Count > 0 && response.Content[0].TryPickText(out var textBlock))
					return textBlock.Text;

				return null;
			}
			catch (AnthropicUnauthorizedException ex)
			{
				_logger.LogError(ex, "[CLAUDE] Unauthorized error calling Anthropic API");
				return null;
			}
			catch (AnthropicUnprocessableEntityException ex)
			{
				_logger.LogError(ex, "[CLAUDE] Unprocessable entity error calling Anthropic API");
				return null;
			}
			catch (AnthropicRateLimitException ex)
			{
				_logger.LogError(ex, "[CLAUDE] Rate limit error calling Anthropic API");
				return null;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[CLAUDE] Unexpected error calling Anthropic API");
				return null;
			}
		}
	}
}
