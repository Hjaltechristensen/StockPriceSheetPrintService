namespace StockPriceSheetPrintService.Service.Models
{
	public class EodResponse
	{
		public List<EodDatum> Data { get; set; } = new();
	}
}
