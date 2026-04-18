using static StockPriceSheetPrintService.Outbound.Filesystem.JsonJuneStore;

namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IJuneStore
	{
		Task<JuneAmountData> GetJuneSharesAmount();
		Task SetJuneSharesAmount(decimal amount);
	}
}
