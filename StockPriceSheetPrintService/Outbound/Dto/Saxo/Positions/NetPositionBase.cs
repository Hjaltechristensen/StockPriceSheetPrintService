namespace StockPriceSheetPrintService.Outbound.Dto.Saxo.Positions
{
	public class NetPositionBase
	{
		public string AssetType { get; set; } = default!;
		public int Uic { get; set; }
	}
}
