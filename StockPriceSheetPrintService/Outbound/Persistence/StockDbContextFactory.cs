using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StockPriceSheetPrintService.Outbound.Persistence
{
	// Used by EF Core tools (Add-Migration) at design time only – no real DB connection needed.
	public class StockDbContextFactory : IDesignTimeDbContextFactory<StockDbContext>
	{
		public StockDbContext CreateDbContext(string[] args)
		{
			var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
				?? "Host=localhost;Database=design_time;Username=design_time;Password=design_time";

			var options = new DbContextOptionsBuilder<StockDbContext>()
				.UseNpgsql(connectionString)
				.Options;

			return new StockDbContext(options);
		}
	}
}
