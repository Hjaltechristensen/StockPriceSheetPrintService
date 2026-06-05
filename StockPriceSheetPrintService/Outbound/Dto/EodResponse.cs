namespace StockPriceSheetPrintService.Outbound.Dto
{
	public class EodResponse
	{
		public List<EodDatum> Data { get; set; } = new();
	}
}
