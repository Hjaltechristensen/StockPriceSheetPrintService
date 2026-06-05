using Microsoft.EntityFrameworkCore;
using StockPriceSheetPrintService.Outbound.Persistence.Entities;
using StockPriceSheetPrintService.Service.Exceptions;
using StockPriceSheetPrintService.Outbound.Mappers;
using StockPriceSheetPrintService.Outbound.Dto;
using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Outbound.Persistence
{
	public class DbNordnetStore(IDbContextFactory<StockDbContext> dbFactory, ILogger<DbNordnetStore> logger) : INordnetStore
	{
		public async Task<CashBalance> GetNordnetCashAmountAsync()
		{
			try
			{
				await using var db = await dbFactory.CreateDbContextAsync();
				var entity = await db.NordnetCash.FindAsync(1);
				var dto = entity != null
					? new NordnetCashJson(entity.CashAmount, entity.LastUpdated)
					: new NordnetCashJson(0m, DateTime.UtcNow);
				return NordnetMapper.ToCashBalance(dto);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error reading NordnetCash from database");
				throw new NordnetStoreException("Could not get cash amount.", ex);
			}
		}

		public async Task SetNordnetCashAmountAsync(decimal newAmount)
		{
			try
			{
				await using var db = await dbFactory.CreateDbContextAsync();
				var entity = await db.NordnetCash.FindAsync(1);
				if (entity == null)
					db.NordnetCash.Add(new NordnetCashEntity { Id = 1, CashAmount = newAmount, LastUpdated = DateTime.UtcNow });
				else
				{
					entity.CashAmount = newAmount;
					entity.LastUpdated = DateTime.UtcNow;
				}
				await db.SaveChangesAsync();
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error writing NordnetCash to database");
				throw new NordnetStoreException("Could not save cash amount.", ex);
			}
		}
	}
}
