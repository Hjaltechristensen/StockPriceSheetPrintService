using Microsoft.EntityFrameworkCore;
using StockPriceSheetPrintService.Outbound.Persistence.Entities;
using StockPriceSheetPrintService.Service.Ports.Persistence;

namespace StockPriceSheetPrintService.Outbound.Persistence
{
	public class DbSeenTransferStore(IDbContextFactory<StockDbContext> dbFactory) : ISeenTransferStore
	{
		private static readonly TimeSpan Retention = TimeSpan.FromDays(14);

		public async Task<HashSet<string>> LoadAsync(CancellationToken ct)
		{
			await using var db = await dbFactory.CreateDbContextAsync(ct);
			var cutoff = DateTime.UtcNow - Retention;
			return await db.SeenTransfers
				.Where(e => e.SeenAt >= cutoff)
				.Select(e => e.BookingId)
				.ToHashSetAsync(ct);
		}

		public async Task SaveAsync(IEnumerable<string> newIds, CancellationToken ct)
		{
			await using var db = await dbFactory.CreateDbContextAsync(ct);
			var cutoff = DateTime.UtcNow - Retention;

			await db.SeenTransfers
				.Where(e => e.SeenAt < cutoff)
				.ExecuteDeleteAsync(ct);

			var existing = await db.SeenTransfers.Select(e => e.BookingId).ToHashSetAsync(ct);
			foreach (var id in newIds.Where(id => !existing.Contains(id)))
				db.SeenTransfers.Add(new SeenTransferEntity { BookingId = id, SeenAt = DateTime.UtcNow });

			await db.SaveChangesAsync(ct);
		}
	}
}
