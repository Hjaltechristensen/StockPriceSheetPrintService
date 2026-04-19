using StockPriceSheetPrintService.Service.Helpers;
using StockPriceSheetPrintService.Service.Ports.Persistence;

namespace StockPriceSheetPrintService.Outbound.Filesystem
{
	public class EncryptedFileTokenStore(IConfiguration configuration, ILogger<EncryptedFileTokenStore> logger) : ITokenStore
	{
		private readonly ILogger<EncryptedFileTokenStore> _logger = logger;
		private readonly string _filePath = configuration["RefreshToken:FilePath"] ?? throw new InvalidOperationException("RefreshToken:FilePath missing");
		private readonly string _encryptionKey = configuration["Saxo:EncryptionKey"] ?? throw new InvalidOperationException("Saxo:EncryptionKey missing");

		public bool TokenExists() => File.Exists(_filePath);

		public async Task<string?> ReadRefreshTokenAsync(CancellationToken ct)
		{
			if (!TokenExists())
			{
				_logger.LogWarning("[TOKEN-STORE] No token found at: {path}", _filePath);
				return null;
			}

			var encrypted = await File.ReadAllTextAsync(_filePath, ct);
			try
			{
				return TokenEncryptor.Decrypt(encrypted, _encryptionKey);
			}
			catch (FormatException ex)
			{
				_logger.LogWarning(ex, "[TOKEN-STORE] Corrupt token-file (invalid base64) – deleting and requiring new login");
				File.Delete(_filePath);
				return null;
			}
		}

		public async Task SaveRefreshTokenAsync(string refreshToken, CancellationToken ct)
		{
			var encrypted = TokenEncryptor.Encrypt(refreshToken, _encryptionKey);

			var dir = Path.GetDirectoryName(_filePath);
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir!);

			await File.WriteAllTextAsync(_filePath, encrypted, ct);
			_logger.LogInformation("[TOKEN-STORE] Refresh token saved at: {path}", _filePath);
		}
	}
}
