using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface ISaxoAccountService
	{
		Task<AccountBalance?> GetBalanceAsync(string accessToken, ClientContext ctx, CancellationToken ct);
		Task<List<Transfer>> GetSaxoTransactionsAsync(string accessToken, DateTime fromDate, DateTime toDate, ClientContext ctx, CancellationToken ct);
		Task<List<Instrument>> GetNetPositionsAsync(string accessToken, ClientContext ctx, CancellationToken ct);
	}
}
