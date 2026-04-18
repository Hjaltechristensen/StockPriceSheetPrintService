using StockPriceSheetPrintService.Outbound.Filesystem.Helper;
using StockPriceSheetPrintService.Service.Ports.Persistence;
using System.Text.Json;

namespace StockPriceSheetPrintService.Outbound.Filesystem
{
	public class SeenTransferStore(IConfiguration configuration, ILogger<SeenTransferStore> logger) : ISeenTransferStore
	{
		private static readonly TimeSpan Retention = TimeSpan.FromDays(14);
		private readonly string _filePath = configuration["SeenTransfers:FilePath"]
			?? throw new InvalidOperationException("SeenTransfers:FilePath missing");
		private readonly ILogger<SeenTransferStore> _logger = logger;
		public async Task<HashSet<string>> LoadAsync(CancellationToken ct)
		{
			if (!File.Exists(_filePath))
			{
				_logger.LogInformation("Seen transfers file not found at '{FilePath}'. Returning an empty set.", _filePath);
				return [];
			}

			var json = await File.ReadAllTextAsync(_filePath, ct);
			var entries = JsonSerializer.Deserialize<List<SeenEntry>>(json) ?? [];
			return entries.Select(e => e.BookingId).ToHashSet();
		}

		public async Task SaveAsync(IEnumerable<string> newIds, CancellationToken ct)
		{
			var cutoff = DateTime.UtcNow - Retention;

			// Læs eksisterende entries for at bevare datoer + ryd gamle op
			List<SeenEntry> entries = [];
			if (File.Exists(_filePath))
			{
				var json = await File.ReadAllTextAsync(_filePath, ct);
				entries = JsonSerializer.Deserialize<List<SeenEntry>>(json) ?? [];
				entries = entries.Where(e => e.SeenAt >= cutoff).ToList(); // cleanup
			}

			foreach (var id in newIds)
				entries.Add(new SeenEntry(id, DateTime.UtcNow));

			// Write to a temp file and atomically replace the original to avoid corruption
			await JsonFileHelper.WriteAtomicAsync(_filePath, entries, ct);
		}

		private record SeenEntry(string BookingId, DateTime SeenAt);
	}
}
