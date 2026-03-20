using StockPriceSheetPrintService.Service.Helpers;
using StockPriceSheetPrintService.Service.Ports;

namespace StockPriceSheetPrintService.Outbound.Filesystem
{
	public class EncryptedFileTokenStore(IConfiguration configuration, ILogger<EncryptedFileTokenStore> logger) : ITokenStore
	{
		private const string TokenPath = "/app/data/refresh_token.bin";
		private readonly ILogger<EncryptedFileTokenStore> _logger = logger;
		private readonly string _encryptionKey = configuration["Saxo:EncryptionKey"]
				?? throw new InvalidOperationException("Saxo:EncryptionKey missing");

		public bool TokenExists() => File.Exists(TokenPath);

		public async Task<string?> ReadRefreshTokenAsync(CancellationToken ct)
		{
			if (!TokenExists())
			{
				_logger.LogWarning("[TOKEN-STORE] Ingen token fundet på: {path}", TokenPath);
				return null;
			}

			var encrypted = await File.ReadAllTextAsync(TokenPath, ct);
			return TokenEncryptor.Decrypt(encrypted, _encryptionKey);
		}

		public async Task SaveRefreshTokenAsync(string refreshToken, CancellationToken ct)
		{
			var encrypted = TokenEncryptor.Encrypt(refreshToken, _encryptionKey);

			var dir = Path.GetDirectoryName(TokenPath);
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir!);

			await File.WriteAllTextAsync(TokenPath, encrypted, ct);
			_logger.LogInformation("[TOKEN-STORE] Refresh token gemt på: {path}", TokenPath);
		}
	}
}
