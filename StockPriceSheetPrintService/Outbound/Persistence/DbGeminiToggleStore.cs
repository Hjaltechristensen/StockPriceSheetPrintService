using Microsoft.EntityFrameworkCore;
using StockPriceSheetPrintService.Outbound.Persistence.Entities;
using StockPriceSheetPrintService.Service.Exceptions;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Outbound.Persistence
{
	public class DbGeminiToggleStore(IDbContextFactory<StockDbContext> dbFactory, ILogger<DbGeminiToggleStore> logger) : IGeminiToggle
	{
		public async Task<bool> IsEnabledAsync()
		{
			try
			{
				await using var db = await dbFactory.CreateDbContextAsync();
				var entity = await db.GeminiToggle.FindAsync(1);
				return entity?.IsEnabled ?? true;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error reading GeminiToggle from database");
				throw new GeminiToggleStoreException("Could not get Gemini toggle state.", ex);
			}
		}

		public async Task ToggleAsync()
		{
			try
			{
				await using var db = await dbFactory.CreateDbContextAsync();
				var entity = await db.GeminiToggle.FindAsync(1);
				if (entity == null)
					db.GeminiToggle.Add(new GeminiToggleEntity { Id = 1, IsEnabled = false });
				else
					entity.IsEnabled = !entity.IsEnabled;
				await db.SaveChangesAsync();
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error writing GeminiToggle to database");
				throw new GeminiToggleStoreException("Could not save Gemini toggle state.", ex);
			}
		}
	}
}
