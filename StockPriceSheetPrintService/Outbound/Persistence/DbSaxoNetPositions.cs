using Microsoft.EntityFrameworkCore;
using StockPriceSheetPrintService.Outbound.Persistence.Entities;
using StockPriceSheetPrintService.Service.Exceptions;
using StockPriceSheetPrintService.Service.Models.Saxo.InstrumentDetails;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Outbound.Persistence
{
	public class DbSaxoNetPositions(IDbContextFactory<StockDbContext> dbFactory) : ISaxoNetPositionStore
	{
		public async Task UpsertPositionsAsync(List<SaxoInstrument> instruments)
		{
			try
			{
				await using var db = await dbFactory.CreateDbContextAsync();

				var uics = instruments.Select(x => x.Uic).ToList();

				var existing = await db.SaxoPositions
					.Where(x => uics.Contains(x.Uic))
					.ToListAsync();

				foreach (var instrument in instruments)
				{
					var normalized = instrument.AssetType.ToUpperInvariant();

					var entity = existing.FirstOrDefault(x => x.Uic == instrument.Uic);

					if (entity == null)
					{
						db.SaxoPositions.Add(new SaxoPositionsEntity
						{
							Uic = instrument.Uic,
							AssetType = normalized,
							Description = instrument.Description,
							Symbol = instrument.Symbol,
							CurrencyCode = instrument.CurrencyCode,
							Exchange = instrument.Exchange
						});
					}
					else
					{
						entity.AssetType = normalized;
						entity.Description = instrument.Description;
						entity.Symbol = instrument.Symbol;
						entity.CurrencyCode = instrument.CurrencyCode;
						entity.Exchange = instrument.Exchange;
					}
				}

				await db.SaveChangesAsync();
			}
			catch (Exception ex)
			{
				throw new SaxoPositionStoreException("Failed to upsert positions set.", ex);
			}
		}

		public async Task<List<SaxoPositionsEntity>> GetNetPositionsAsync()
		{
			try
			{
				await using var db = await dbFactory.CreateDbContextAsync();

				var positions = await db.SaxoPositions.ToListAsync();

				return positions.Count > 0 ? positions : [];
			}
			catch (Exception ex)
			{
				throw new SaxoPositionStoreException("Failed to read uic store.", ex);
			}
		}

		public async Task RemoveAllPositionsAsync()
		{
			try
			{
				await using var db = await dbFactory.CreateDbContextAsync();

				await db.SaxoPositions.ExecuteDeleteAsync();
			}
			catch (Exception ex)
			{
				throw new SaxoPositionStoreException("Failed to remove all positions.", ex);
			}
		}
	}
}
