using StockPriceSheetPrintService.Service.Models.Saxo;
using StockPriceSheetPrintService.Service.Models.Saxo.Transactions;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface ISaxoAccountService
	{
		Task<SaxoBalanceResponse?> GetBalanceAsync(string accessToken, CancellationToken ct);
		Task<SaxoTransactionsResponse> GetSaxoTransactionsAsync(string accessToken, DateTime fromDate, DateTime toDate, CancellationToken ct);
	}
}
