using StockPriceSheetPrintService.Outbound.Filesystem.Helper;
using StockPriceSheetPrintService.Service.Exceptions;
using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports.Outbound;
using System.Text.Json;

namespace StockPriceSheetPrintService.Outbound.Filesystem
{
	public class JsonJuneStore(IConfiguration configuration, ILogger<JsonJuneStore> logger) : IJuneStore
	{
		private readonly string _filePath = configuration["JuneSharesAmount:FilePath"] ?? throw new InvalidOperationException("JuneSharesAmount:FilePath is missing");
		private readonly ILogger<JsonJuneStore> _logger = logger;

		public async Task<JuneAmountData> GetJuneSharesAmount()
		{
			try
			{
				if (!File.Exists(_filePath))
				{
					_logger.LogWarning("JuneSharesAmount file not found at '{FilePath}'. Returning defaults.", _filePath);
					return new JuneAmountData(0m, DateTime.UtcNow);
				}
				var json = await File.ReadAllTextAsync(_filePath);
				var entries = JsonSerializer.Deserialize<JuneAmountData>(json);
				if (entries == null)
				{
					_logger.LogWarning("JuneSharesAmount deserialized as null, returning defaults.");
					return new JuneAmountData(0m, DateTime.UtcNow);
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
				await JsonFileHelper.WriteAtomicAsync(_filePath, new JuneAmountData(amount, DateTime.UtcNow));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error writing to JuneSharesAmount file");
				throw new JuneStoreException("Could not save June shares amount.", ex);
			}
		}
	}
}
