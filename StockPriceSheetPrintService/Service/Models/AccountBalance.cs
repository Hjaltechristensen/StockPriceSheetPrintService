namespace StockPriceSheetPrintService.Service.Models
{
	public record AccountBalance(decimal TotalValue, decimal CashBalance, string Currency, decimal AssetValue);
}
