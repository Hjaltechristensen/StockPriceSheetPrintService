using StockPriceSheetPrintService.Outbound.Filesystem.Helpers;
using StockPriceSheetPrintService.Service.Exceptions;
using StockPriceSheetPrintService.Outbound.Dto;
using StockPriceSheetPrintService.Outbound.Mappers;
using StockPriceSheetPrintService.Service.Models;
using StockPriceSheetPrintService.Service.Ports.Outbound;
using System.Text.Json;

namespace StockPriceSheetPrintService.Outbound.Filesystem
{
	public class JsonNordnetStore(IConfiguration configuration, ILogger<JsonNordnetStore> logger) : INordnetStore
	{
		private readonly string _filePath = configuration["NordnetCash:FilePath"] ?? throw new InvalidOperationException("NordnetCash:FilePath is missing");
		private readonly ILogger<JsonNordnetStore> _logger = logger;

		public async Task<CashBalance> GetNordnetCashAmountAsync()
		{
			try
			{
				if (!File.Exists(_filePath))
				{
					_logger.LogWarning("NordnetCash file not found at '{FilePath}. Returning defaults.", _filePath);
					return NordnetMapper.ToCashBalance(new NordnetCashJson(0m, DateTime.UtcNow));
				}
				var json = await File.ReadAllTextAsync(_filePath);
				var entries = JsonSerializer.Deserialize<NordnetCashJson>(json);
				if (entries == null)
				{
					_logger.LogWarning("NordnetCash deserialized as null, returning defaults.");
					return NordnetMapper.ToCashBalance(new NordnetCashJson(0m, DateTime.UtcNow));
				}

				return NordnetMapper.ToCashBalance(entries);
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
				await JsonFileHelper.WriteAtomicAsync(_filePath, new NordnetCashJson(newAmount, DateTime.UtcNow));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error writing to NordnetCash file");
				throw new NordnetStoreException("Could not save cash amount.", ex);
			}
		}
	}
}
