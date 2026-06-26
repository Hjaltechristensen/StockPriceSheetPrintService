namespace StockPriceSheetPrintService.Service
{
	public static class ClientContextFactory
	{
		public static ClientContext New(string source) =>
			new(Guid.NewGuid(), source, DateTimeOffset.UtcNow);
	}
}
