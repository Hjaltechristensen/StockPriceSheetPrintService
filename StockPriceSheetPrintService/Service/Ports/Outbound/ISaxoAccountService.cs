using StockPriceSheetPrintService.OutboundDto.Saxo;
using StockPriceSheetPrintService.OutboundDto.Saxo.InstrumentDetails;
using StockPriceSheetPrintService.OutboundDto.Saxo.Transactions;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface ISaxoAccountService
	{
		Task<SaxoBalanceResponse?> GetBalanceAsync(string accessToken, CancellationToken ct);
		Task<SaxoTransactionsResponse> GetSaxoTransactionsAsync(string accessToken, DateTime fromDate, DateTime toDate, CancellationToken ct);
		Task<List<SaxoInstrument>> GetNetPositionsAsync(string accessToken, CancellationToken ct);
	}
}
