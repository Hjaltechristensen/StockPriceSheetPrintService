using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface ISaxoNetPositionStore
	{
		Task<List<Instrument>> GetNetPositionsAsync();
		Task UpsertPositionsAsync(List<Instrument> instruments);
		Task RemoveStalePositionsAsync(List<int> validUics);
	}
}
