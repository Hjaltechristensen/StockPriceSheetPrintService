namespace StockPriceSheetPrintService.Service.Ports
{
	public interface ISeenTransferStore
	{
		Task<HashSet<string>> LoadAsync(CancellationToken ct);
		Task SaveAsync(IEnumerable<string> newIds, CancellationToken ct);
	}
}
