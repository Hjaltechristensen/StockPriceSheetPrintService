namespace StockPriceSheetPrintService.Outbound.Dto
{
	public record PendingReport(
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
