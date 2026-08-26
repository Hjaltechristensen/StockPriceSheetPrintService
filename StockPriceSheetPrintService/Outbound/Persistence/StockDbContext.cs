using Microsoft.EntityFrameworkCore;
using StockPriceSheetPrintService.Outbound.Persistence.Entities;
using StockPriceSheetPrintService.Outbound.Dto.Saxo.InstrumentDetails;
using System.Text.Json;

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
		public DbSet<GeminiToggleEntity> GeminiToggle { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<SeenTransferEntity>().HasKey(e => e.BookingId);
			modelBuilder.Entity<NordnetSymbolEntity>().HasKey(e => e.Ticker);
			modelBuilder.Entity<SaxoPositionsEntity>().HasKey(e => e.Uic);
			modelBuilder.Entity<SaxoPositionsEntity>()
				.Property(e => e.Exchange)
				.HasColumnType("jsonb")
				.HasConversion(
					v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
					v => JsonSerializer.Deserialize<ExchangeDto>(v, (JsonSerializerOptions?)null) ?? new ExchangeDto());
		}
	}
}
