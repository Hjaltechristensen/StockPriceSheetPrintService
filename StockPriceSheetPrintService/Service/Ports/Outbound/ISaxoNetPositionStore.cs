using StockPriceSheetPrintService.OutboundDto.Saxo.InstrumentDetails;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface ISaxoNetPositionStore
	{
		Task<List<SaxoInstrument>> GetNetPositionsAsync();
		Task UpsertPositionsAsync(List<SaxoInstrument> instruments);
		Task RemoveStalePositionsAsync(List<int> validUics);
	}
}
