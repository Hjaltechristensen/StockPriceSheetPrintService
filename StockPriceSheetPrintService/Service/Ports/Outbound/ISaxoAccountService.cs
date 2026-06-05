using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface ISaxoAccountService
	{
		Task<AccountBalance?> GetBalanceAsync(string accessToken, CancellationToken ct);
		Task<List<Transfer>> GetSaxoTransactionsAsync(string accessToken, DateTime fromDate, DateTime toDate, CancellationToken ct);
		Task<List<Instrument>> GetNetPositionsAsync(string accessToken, CancellationToken ct);
	}
}
