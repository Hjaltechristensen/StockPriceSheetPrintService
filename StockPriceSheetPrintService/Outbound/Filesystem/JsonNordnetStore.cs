using StockPriceSheetPrintService.Outbound.Filesystem.Helper;
using StockPriceSheetPrintService.Service.Exceptions;
using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports.Outbound;
using System.Text.Json;

namespace StockPriceSheetPrintService.Outbound.Filesystem
{
	public class JsonNordnetStore(IConfiguration configuration, ILogger<JsonNordnetStore> logger) : INordnetStore
	{
		private readonly string _filePath = configuration["NordnetCash:FilePath"] ?? throw new InvalidOperationException("NordnetCash:FilePath is missing");
		private readonly ILogger<JsonNordnetStore> _logger = logger;
		public async Task<NordnetCashJson> GetNordnetCashAmountAsync()
		{
			try
			{
				if (!File.Exists(_filePath))
				{
					_logger.LogWarning("NordnetCash file not found at '{FilePath}. Returning defaults.", _filePath);
					return new NordnetCashJson(0m, DateTime.Now);
				}
				var json = await File.ReadAllTextAsync(_filePath);
				var entries = JsonSerializer.Deserialize<NordnetCashJson>(json);
				if (entries == null)
				{
					_logger.LogWarning("Entries is null, returning decimal 0 and DateTime.Now");
					return new NordnetCashJson(0m, DateTime.Now);
				}

				return entries;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error reading NordnetCash file");
				throw new NordnetStoreException("Could not get cash amount.", ex);
			}
		}

		public async Task SetNordnetCashAmountAsync(decimal newAmount)
		{
			try
			{
				var entries = new NordnetCashJson(newAmount, DateTime.UtcNow);
				await JsonFileHelper.WriteAtomicAsync(_filePath, entries);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error writing to NordnetCash file");
				throw new NordnetStoreException("Could not save cash amount.", ex);
			}
		}

		public record NordnetCashJson(decimal CashAmount, DateTime LastUpdated) { }
	}
}
