using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using StockPriceSheetPrintService.Service.Models.Saxo.Transactions;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Outbound.ClaudeInsights
{
	public class ClaudeReportInsightsImpl(IConfiguration configuration, ILogger logger) : IClaudeReportInsights
	{
		private readonly ILogger _logger = logger;
		private readonly string _claudeApiKey = configuration["Claude:ApiKey"] ?? throw new InvalidOperationException("ClaudeApiKey missing");

		public async Task<string?> GetInsightsAsync(
			decimal saxoBalance,
			decimal nordnetValue,
			decimal juneValue,
			decimal total,
			decimal previousDayValue,
			List<SaxoTransaction> newTransfers,
			List<string> tickers,
			CancellationToken ct)
		{
			AnthropicClient client = new() { ApiKey = _claudeApiKey };

			MessageCreateParams parameters = new()
			{
				MaxTokens = 1024,
				Messages =
				[
					new()
					{
						Role = Role.User,
						Content = $"Hello, Claude",
					},
				],
				Model = "claude-haiku-4-5",
			};

			try
			{
				var response = await client.Messages.Create(parameters);
				response.Validate();

				_logger.LogInformation("Received response from Anthropic API.\nTotal tokens used: {TotalTokens}", response.Usage);

				if (response.Content[0].TryPickText(out var textBlock))
				{
					return textBlock.Text;
				}

				return null;

			}
			catch (AnthropicUnauthorizedException ex)
			{
				_logger.LogError($"Anthropic Unauthorized error: {ex.Message}");
				return null;
			}
			catch (AnthropicUnprocessableEntityException ex)
			{
				_logger.LogError($"Anthropic Unprocessable Entity error: {ex.Message}");
				return null;

			}
			catch (AnthropicRateLimitException ex)
			{
				_logger.LogError($"Anthropic Rate Limit error: {ex.Message}");
				return null;
			}
			catch (Exception)
			{
				_logger.LogError("An unexpected error occurred while calling Anthropic API.");
				return null;
			}
		}
	}
}
