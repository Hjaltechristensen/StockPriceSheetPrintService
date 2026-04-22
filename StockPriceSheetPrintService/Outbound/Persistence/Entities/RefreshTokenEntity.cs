namespace StockPriceSheetPrintService.Outbound.Persistence.Entities
{
	public class RefreshTokenEntity
	{
		public int Id { get; set; }
		public string EncryptedToken { get; set; } = "";
		public DateTime UpdatedAt { get; set; }
	}
}
