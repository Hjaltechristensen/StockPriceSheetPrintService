using System.Text.Json;

namespace StockPriceSheetPrintService.Outbound.Filesystem.Helpers
{
	internal static class JsonFileHelper
	{
		public static async Task WriteAtomicAsync<T>(string filePath, T value, CancellationToken ct = default)
		{
			var tempFile = Path.GetTempFileName();
			await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(value), ct);
			File.Move(tempFile, filePath, overwrite: true);
		}
	}
}
