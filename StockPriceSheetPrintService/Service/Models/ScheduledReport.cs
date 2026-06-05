namespace StockPriceSheetPrintService.Service.Models
{
	public record ScheduledReport(
		decimal SaxoBalance,
		decimal NordnetValue,
		decimal JuneValue,
		decimal Total,
		decimal PreviousDayValue,
		decimal? TransferAmount,
		string? GeminiInsights,
		DateTime ScheduledAtUtc
	);
}
