using Microsoft.EntityFrameworkCore;
using StockPriceSheetPrintService.Outbound.Dto.Saxo.InstrumentDetails;
using StockPriceSheetPrintService.Outbound.Mappers;
using StockPriceSheetPrintService.Outbound.Persistence.Entities;
using StockPriceSheetPrintService.Service.Exceptions;
using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Outbound.Persistence
{
	public class DbSaxoNetPositions(IDbContextFactory<StockDbContext> dbFactory) : ISaxoNetPositionStore
	{
		public async Task UpsertPositionsAsync(List<Instrument> instruments)
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
					var dto = SaxoMapper.ToDto(instrument);
					var normalized = dto.AssetType.ToUpperInvariant();

					var entity = existing.FirstOrDefault(x => x.Uic == dto.Uic);

					if (entity == null)
					{
						db.SaxoPositions.Add(new SaxoPositionsEntity
						{
							Uic = dto.Uic,
							AssetType = normalized,
							Description = dto.Description,
							Symbol = dto.Symbol,
							CurrencyCode = dto.CurrencyCode,
							Exchange = dto.Exchange
						});
					}
					else
					{
						entity.AssetType = normalized;
						entity.Description = dto.Description;
						entity.Symbol = dto.Symbol;
						entity.CurrencyCode = dto.CurrencyCode;
						entity.Exchange = dto.Exchange;
					}
				}

				await db.SaveChangesAsync();
			}
			catch (Exception ex)
			{
				throw new SaxoPositionStoreException("Failed to upsert positions set.", ex);
			}
		}

		public async Task<List<Instrument>> GetNetPositionsAsync()
		{
			try
			{
				await using var db = await dbFactory.CreateDbContextAsync();

				var positions = await db.SaxoPositions.ToListAsync();

				return positions
					.Select(p => SaxoMapper.ToInstrument(new SaxoInstrument
					{
						Uic = p.Uic,
						AssetType = p.AssetType,
						Description = p.Description,
						Symbol = p.Symbol,
						CurrencyCode = p.CurrencyCode,
						Exchange = p.Exchange
					}))
					.ToList();
			}
			catch (Exception ex)
			{
				throw new SaxoPositionStoreException("Failed to read uic store.", ex);
			}
		}

		public async Task RemoveStalePositionsAsync(List<int> validUics)
		{
			try
			{
				await using var db = await dbFactory.CreateDbContextAsync();

				await db.SaxoPositions
					.Where(x => !validUics.Contains(x.Uic))
					.ExecuteDeleteAsync();
			}
			catch (Exception ex)
			{
				throw new SaxoPositionStoreException("Failed to remove stale positions.", ex);
			}
		}
	}
}
