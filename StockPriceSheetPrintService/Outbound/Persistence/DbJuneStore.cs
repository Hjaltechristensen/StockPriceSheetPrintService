using Microsoft.EntityFrameworkCore;
using StockPriceSheetPrintService.Outbound.Persistence.Entities;
using StockPriceSheetPrintService.OutboundExceptions;
using StockPriceSheetPrintService.OutboundMappers;
using StockPriceSheetPrintService.OutboundDto;
using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Outbound.Persistence
{
	public class DbJuneStore(IDbContextFactory<StockDbContext> dbFactory, ILogger<DbJuneStore> logger) : IJuneStore
	{
		public async Task<FundHolding> GetJuneSharesAmountAsync()
		{
			try
			{
				await using var db = await dbFactory.CreateDbContextAsync();
				var entity = await db.JuneShares.FindAsync(1);
				var dto = entity != null
					? new JuneAmountData(entity.Amount, entity.LastUpdated)
					: new JuneAmountData(0m, DateTime.UtcNow);
				return JuneMapper.ToFundHolding(dto);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error reading JuneShares from database");
				throw new JuneStoreException("Could not get June shares amount.", ex);
			}
		}

		public async Task SetJuneSharesAmountAsync(decimal amount)
		{
			try
			{
				await using var db = await dbFactory.CreateDbContextAsync();
				var entity = await db.JuneShares.FindAsync(1);
				if (entity == null)
					db.JuneShares.Add(new JuneSharesEntity { Id = 1, Amount = amount, LastUpdated = DateTime.UtcNow });
				else
				{
					entity.Amount = amount;
					entity.LastUpdated = DateTime.UtcNow;
				}
				await db.SaveChangesAsync();
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error writing JuneShares to database");
				throw new JuneStoreException("Could not save June shares amount.", ex);
			}
		}
	}
}
