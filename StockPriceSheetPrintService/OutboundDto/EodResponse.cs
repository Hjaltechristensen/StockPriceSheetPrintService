namespace StockPriceSheetPrintService.OutboundDto
{
	public class EodResponse
	{
		public List<EodDatum> Data { get; set; } = new();
	}
}
