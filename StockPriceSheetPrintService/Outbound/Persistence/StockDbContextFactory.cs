using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StockPriceSheetPrintService.Outbound.Persistence
{
	public class StockDbContextFactory : IDesignTimeDbContextFactory<StockDbContext>
	{
		public StockDbContext CreateDbContext(string[] args)
		{
			var options = new DbContextOptionsBuilder<StockDbContext>()
				.UseNpgsql("Host=localhost;Port=5432;Database=stockpricedb;Username=stockuser;Password=123456")
				.Options;

			return new StockDbContext(options);
		}
	}
}
