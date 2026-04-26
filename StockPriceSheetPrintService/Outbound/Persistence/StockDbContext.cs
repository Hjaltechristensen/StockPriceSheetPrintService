using Microsoft.EntityFrameworkCore;
using StockPriceSheetPrintService.Outbound.Persistence.Entities;

namespace StockPriceSheetPrintService.Outbound.Persistence
{
	public class StockDbContext(DbContextOptions<StockDbContext> options) : DbContext(options)
	{
		public DbSet<RefreshTokenEntity> RefreshTokens { get; set; }
		public DbSet<SeenTransferEntity> SeenTransfers { get; set; }
		public DbSet<NordnetCashEntity> NordnetCash { get; set; }
		public DbSet<JuneSharesEntity> JuneShares { get; set; }
		public DbSet<NordnetSymbolEntity> NordnetSymbols { get; set; }
		public DbSet<ExecutionLogEntity> ExecutionLogs { get; set; }
		public DbSet<SaxoPositionsEntity> SaxoPositions { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<SeenTransferEntity>().HasKey(e => e.BookingId);
			modelBuilder.Entity<NordnetSymbolEntity>().HasKey(e => e.Ticker);
			modelBuilder.Entity<SaxoPositionsEntity>().HasKey(e => e.Uic);
		}
	}
}
