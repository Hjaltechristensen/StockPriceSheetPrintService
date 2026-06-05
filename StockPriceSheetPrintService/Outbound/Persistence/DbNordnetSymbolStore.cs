using Microsoft.EntityFrameworkCore;
using StockPriceSheetPrintService.Outbound.Persistence.Entities;
using StockPriceSheetPrintService.OutboundExceptions;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Outbound.Persistence
{
	public class DbNordnetSymbolStore(IDbContextFactory<StockDbContext> dbFactory) : INordnetSymbolStore
	{
		public async Task<Dictionary<string, decimal>> GetSymbolsAsync()
		{
			try
			{
				await using var db = await dbFactory.CreateDbContextAsync();
				var symbols = await db.NordnetSymbols.ToDictionaryAsync(e => e.Ticker, e => e.Shares);
				return symbols.Count > 0 ? symbols : GetDefaults();
			}
			catch (Exception ex)
			{
				throw new NordnetSymbolStoreException("Failed to read symbol store.", ex);
			}
		}

		public async Task AddOrUpdateSymbolAsync(string ticker, decimal shares)
		{
			try
			{
				await using var db = await dbFactory.CreateDbContextAsync();
				var normalized = ticker.ToUpperInvariant();
				var entity = await db.NordnetSymbols.FindAsync(normalized);
				if (entity == null)
					db.NordnetSymbols.Add(new NordnetSymbolEntity { Ticker = normalized, Shares = shares });
				else
					entity.Shares = shares;
				await db.SaveChangesAsync();
			}
			catch (Exception ex)
			{
				throw new NordnetSymbolStoreException($"Failed to add/update symbol '{ticker}'.", ex);
			}
		}

		public async Task RemoveSymbolAsync(string ticker)
		{
			try
			{
				await using var db = await dbFactory.CreateDbContextAsync();
				var normalized = ticker.ToUpperInvariant();
				var entity = await db.NordnetSymbols.FindAsync(normalized);
				if (entity == null)
					throw new NordnetSymbolStoreException($"Ticker '{ticker}' not found in symbol store.");
				db.NordnetSymbols.Remove(entity);
				await db.SaveChangesAsync();
			}
			catch (NordnetSymbolStoreException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new NordnetSymbolStoreException($"Failed to remove symbol '{ticker}'.", ex);
			}
		}

		private static Dictionary<string, decimal> GetDefaults() => new()
		{
			{ "2B76.DE", 218 },
			{ "IQQQ.DE", 47 },
			{ "O", 40 }
		};
	}
}
