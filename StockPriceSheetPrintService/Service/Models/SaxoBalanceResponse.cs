namespace StockPriceSheetPrintService.Service.Models
{
	public class SaxoBalanceResponse
	{
		public decimal TotalValue { get; set; }
		public decimal CashBalance { get; set; }
		public string Currency { get; set; } = string.Empty;
		public decimal CalculationAssetValue { get; set; }
	}
}
