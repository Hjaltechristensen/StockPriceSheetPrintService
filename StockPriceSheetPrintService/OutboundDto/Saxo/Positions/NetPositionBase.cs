namespace StockPriceSheetPrintService.OutboundDto.Saxo.Positions
{
	public class NetPositionBase
	{
		public string AssetType { get; set; } = default!;
		public int Uic { get; set; }
	}
}
