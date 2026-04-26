using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using StockPriceSheetPrintService.Service.Models.Saxo.InstrumentDetails;
using StockPriceSheetPrintService.Service.Models.Saxo.Transactions;
using StockPriceSheetPrintService.Service.Ports.Outbound;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StockPriceSheetPrintService.Outbound.ClaudeInsights
{
	public class ClaudeReportInsightsImpl(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<ClaudeReportInsightsImpl> logger) : IClaudeReportInsights
	{
		private readonly ILogger<ClaudeReportInsightsImpl> _logger = logger;
		private const string Model = "claude-haiku-4-5-20251001";
		private const int _maxTokens = 1024;

		private readonly string _apiKey = configuration["Claude:ApiKey"] ?? throw new InvalidOperationException("Claude:ApiKey missing");

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

			var requestBody = new
			{
				model = Model,
				max_tokens = _maxTokens,
				system = SystemPrompt,
				tools = new[]
				{
					new { type = "web_search_20250305", name = "web_search" }
				},
				messages = new[]
				{
					new { role = "user", content = userPrompt }
				}
			};

			var json = JsonSerializer.Serialize(requestBody);

			try
			{
				var client = httpClientFactory.CreateClient("Claude");
				using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
				request.Headers.Add("x-api-key", _apiKey);
				request.Headers.Add("anthropic-version", "2023-06-01");
				request.Content = new StringContent(json, Encoding.UTF8, "application/json");

				using var response = await client.SendAsync(request, ct);
				var responseBody = await response.Content.ReadAsStringAsync(ct);

				if (!response.IsSuccessStatusCode)
				{
					logger.LogError("[CLAUDE] API error {StatusCode}: {Body}", response.StatusCode, responseBody);
					return null;
				}

				var doc = JsonNode.Parse(responseBody);
				var usage = doc?["usage"];
				logger.LogInformation(
					"[CLAUDE] Insights received. Tokens: input={InputTokens}, output={OutputTokens}",
					usage?["input_tokens"], usage?["output_tokens"]);

				// Saml alle text-blokke fra content-arrayet (ignorer tool_use og web_search_tool_result)
				var contentArray = doc?["content"]?.AsArray();
				if (contentArray is null) return null;

				var textParts = contentArray
					.Where(block => block?["type"]?.GetValue<string>() == "text")
					.Select(block => block?["text"]?.GetValue<string>())
					.Where(t => !string.IsNullOrWhiteSpace(t));

				return string.Join(" ", textParts).Trim() is { Length: > 0 } result ? result : null;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "[CLAUDE] Unexpected error calling Anthropic API");
				return null;
			}

		}
	}
}
