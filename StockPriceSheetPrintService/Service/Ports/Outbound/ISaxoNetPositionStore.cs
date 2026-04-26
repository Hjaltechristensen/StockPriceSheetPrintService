using StockPriceSheetPrintService.Outbound.Persistence.Entities;
using StockPriceSheetPrintService.Service.Models.Saxo.InstrumentDetails;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface ISaxoNetPositionStore
	{
		Task<List<SaxoPositionsEntity>> GetNetPositionsAsync();
		Task UpsertPositionsAsync(List<SaxoInstrument> instruments);
		Task RemoveAllPositionsAsync();
	}
}
