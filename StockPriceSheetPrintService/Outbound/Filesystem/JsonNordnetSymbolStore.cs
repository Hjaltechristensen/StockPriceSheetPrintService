using StockPriceSheetPrintService.Outbound.Filesystem.Helpers;
using StockPriceSheetPrintService.Service.Exceptions;
using StockPriceSheetPrintService.Service.Ports.Outbound;
using System.Text.Json;

namespace StockPriceSheetPrintService.Outbound.Filesystem
{
	public class JsonNordnetSymbolStore(IConfiguration configuration, ILogger<JsonNordnetSymbolStore> logger) : INordnetSymbolStore
	{
		private readonly string _filePath = configuration["NordnetSymbol:FilePath"] ?? throw new InvalidOperationException("NordnetSymbol:FilePath is missing");
		private readonly ILogger<JsonNordnetSymbolStore> _logger = logger;
		private readonly SemaphoreSlim _lock = new(1, 1);

		public async Task AddOrUpdateSymbolAsync(string ticker, decimal shares)
		{
			await _lock.WaitAsync();
			try
			{
				var symbols = await ReadFromDiskAsync();
				symbols[ticker.ToUpperInvariant()] = shares;
				await WriteToDiskAsync(symbols);
			}
			catch (Exception ex)
			{
				throw new NordnetSymbolStoreException($"Failed to add/update symbol '{ticker}'.", ex);
			}
			finally
			{
				_lock.Release();
			}
		}

		public async Task<Dictionary<string, decimal>> GetSymbolsAsync()
		{
			await _lock.WaitAsync();
			try
			{
				if (!File.Exists(_filePath))
				{
					_logger.LogWarning("NordnetSymbol file not found at '{FilePath}'. Returning defaults.", _filePath);
					return GetDefaults();
				}

				var json = await File.ReadAllTextAsync(_filePath);
				return JsonSerializer.Deserialize<Dictionary<string, decimal>>(json) ?? GetDefaults();
			}
			catch (Exception ex)
			{
				throw new NordnetSymbolStoreException("Failed to read symbol store.", ex);
			}
			finally
			{
				_lock.Release();
			}
		}

		public async Task RemoveSymbolAsync(string ticker)
		{
			await _lock.WaitAsync();
			try
			{
				var symbols = await ReadFromDiskAsync();
				if (!symbols.Remove(ticker.ToUpperInvariant()))
					throw new NordnetSymbolStoreException($"Ticker '{ticker}' not found in symbol store.");
				await WriteToDiskAsync(symbols);
			}
			catch (NordnetSymbolStoreException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new NordnetSymbolStoreException($"Failed to remove symbol '{ticker}'.", ex);
			}
			finally
			{
				_lock.Release();
			}
		}

		private static Dictionary<string, decimal> GetDefaults() => new()
		{
			{ "2B76.DE", 218 },
			{ "IQQQ.DE", 47 },
			{ "O", 40 }
		};

		private async Task<Dictionary<string, decimal>> ReadFromDiskAsync()
		{
			if (!File.Exists(_filePath))
			{
				_logger.LogWarning("NordnetSymbol file not found at '{FilePath}'. Returning defaults.", _filePath);
				return GetDefaults();
			}

			var json = await File.ReadAllTextAsync(_filePath);
			return JsonSerializer.Deserialize<Dictionary<string, decimal>>(json) ?? GetDefaults();
		}

		private async Task WriteToDiskAsync(Dictionary<string, decimal> symbols)
		{
			await JsonFileHelper.WriteAtomicAsync(_filePath, symbols);
		}
	}
}
