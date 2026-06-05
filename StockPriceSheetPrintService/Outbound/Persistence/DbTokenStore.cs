using Microsoft.EntityFrameworkCore;
using StockPriceSheetPrintService.Outbound.Persistence.Entities;
using StockPriceSheetPrintService.Outbound.Helpers;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Outbound.Persistence
{
	public class DbTokenStore(IDbContextFactory<StockDbContext> dbFactory, IConfiguration configuration, ILogger<DbTokenStore> logger) : ITokenStore
	{
		private readonly string _encryptionKey = configuration["Saxo:EncryptionKey"] ?? throw new InvalidOperationException("Saxo:EncryptionKey missing");

		public bool TokenExists()
		{
			using var db = dbFactory.CreateDbContext();
			return db.RefreshTokens.Any();
		}

		public async Task<string?> ReadRefreshTokenAsync(CancellationToken ct)
		{
			await using var db = await dbFactory.CreateDbContextAsync(ct);
			var entity = await db.RefreshTokens.FindAsync([1], ct);
			if (entity == null)
			{
				logger.LogWarning("[TOKEN-STORE] No token found in database");
				return null;
			}
			try
			{
				return TokenEncryptor.Decrypt(entity.EncryptedToken, _encryptionKey);
			}
			catch (FormatException ex)
			{
				logger.LogWarning(ex, "[TOKEN-STORE] Corrupt token in database – deleting and requiring new login");
				db.RefreshTokens.Remove(entity);
				await db.SaveChangesAsync(ct);
				return null;
			}
		}

		public async Task SaveRefreshTokenAsync(string refreshToken, CancellationToken ct)
		{
			var encrypted = TokenEncryptor.Encrypt(refreshToken, _encryptionKey);
			await using var db = await dbFactory.CreateDbContextAsync(ct);
			var entity = await db.RefreshTokens.FindAsync([1], ct);
			if (entity == null)
				db.RefreshTokens.Add(new RefreshTokenEntity { Id = 1, EncryptedToken = encrypted, UpdatedAt = DateTime.UtcNow });
			else
			{
				entity.EncryptedToken = encrypted;
				entity.UpdatedAt = DateTime.UtcNow;
			}
			await db.SaveChangesAsync(ct);
			logger.LogInformation("[TOKEN-STORE] Refresh token saved to database");
		}
	}
}
