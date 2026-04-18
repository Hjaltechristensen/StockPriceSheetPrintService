using StockPriceSheetPrintService.Outbound.Filesystem.Helper;
using StockPriceSheetPrintService.Service.Exceptions;
using StockPriceSheetPrintService.Service.Ports.Outbound;
using System.Text.Json;

namespace StockPriceSheetPrintService.Outbound.Filesystem
{
	public class JsonJuneStore(IConfiguration configuration, ILogger<JsonJuneStore> logger) : IJuneStore
	{
		private readonly string _filePath = configuration["JuneSharesAmount:FilePath"] ?? throw new InvalidOperationException("JuneSharesAmount:FilePath is mising");
		private readonly ILogger<JsonJuneStore> _logger = logger;
		public async Task<JuneAmountData> GetJuneSharesAmount()
		{
			try
			{
				if (!File.Exists(_filePath))
				{
					_logger.LogWarning("JuneSharesAmount file not found at '{FilePath}. Returning defaults.", _filePath);
					return new JuneAmountData(0m, DateTime.Now);
				}
				var json = await File.ReadAllTextAsync(_filePath);
				var entries = JsonSerializer.Deserialize<JuneAmountData>(json);
				if (entries == null)
				{
					_logger.LogWarning("Entries is null, returning decimal 0 and DateTime.Now");
					return new JuneAmountData(0m, DateTime.Now);
				} 
				
				return entries;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error reading JuneSharesAmount file");
				throw new JuneStoreException("Could not get June shares amount.", ex);
			}
		}

		public async Task SetJuneSharesAmount(decimal amount)
		{
			try
			{
				var entries = new JuneAmountData(amount, DateTime.Now);
				await JsonFileHelper.WriteAtomicAsync(_filePath, entries);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error writing to NordnetCash file");
				throw new JuneStoreException("Could not save cash amount.", ex);
			}
		}

		public record JuneAmountData(decimal Amount, DateTime LastUpdated) { }
	}
}
