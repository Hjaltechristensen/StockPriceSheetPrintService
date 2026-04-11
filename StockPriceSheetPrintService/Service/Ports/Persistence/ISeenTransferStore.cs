namespace StockPriceSheetPrintService.Service.Ports.Persistence
{
	public interface ISeenTransferStore
	{
		Task<HashSet<string>> LoadAsync(CancellationToken ct);
		Task SaveAsync(IEnumerable<string> newIds, CancellationToken ct);
	}
}
