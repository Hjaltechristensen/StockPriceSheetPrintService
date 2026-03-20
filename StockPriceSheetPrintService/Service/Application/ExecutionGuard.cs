namespace StockPriceSheetPrintService.Service.Application
{
	public class ExecutionGuard
	{
		private readonly ILogger<ExecutionGuard> _logger;
		private readonly string _executionLogPath = "execution_log.txt";
		private const int MaxExecutionsPerHour = 3;
		private const int MaxExecutionsPerMonth = 100;

		private readonly List<DateTimeOffset> _executionCache = new();
		private readonly object _cacheLock = new();
		private DateTimeOffset _lastFileSyncTime = DateTimeOffset.UtcNow;

		public ExecutionGuard(ILogger<ExecutionGuard> logger)
		{
			_logger = logger;
			LoadExecutionHistoryFromFile();
		}

		public bool IsExecutionSafe()
		{
			try
			{
				var now = DateTimeOffset.UtcNow;

				lock (_cacheLock)
				{
					var executionsThisMonth = _executionCache
						.Count(ts => ts.Year == now.Year && ts.Month == now.Month);

					var executionsLastHour = _executionCache
						.Count(ts => (now - ts).TotalHours < 1);

					if (executionsLastHour >= MaxExecutionsPerHour)
					{
						_logger.LogWarning("Sikkerhedsadvarsel: {count} kørsler på 1 time. Grænse: {limit}",
							executionsLastHour, MaxExecutionsPerHour);
						return false;
					}

					if (executionsThisMonth >= MaxExecutionsPerMonth)
					{
						_logger.LogWarning("Sikkerhedsadvarsel: {count} kørsler denne måned. Grænse: {limit}",
							executionsThisMonth, MaxExecutionsPerMonth);
						return false;
					}
				}

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Fejl ved sikkerhedstjek");
				return true;
			}
		}

		public void LogExecution()
		{
			try
			{
				var now = DateTimeOffset.UtcNow;

				lock (_cacheLock)
				{
					_executionCache.Add(now);
				}

				if ((now - _lastFileSyncTime).TotalHours > 1)
				{
					SyncExecutionCacheToFile();
					_lastFileSyncTime = now;
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Fejl ved logning af kørsel");
			}
		}

		private void LoadExecutionHistoryFromFile()
		{
			try
			{
				if (!File.Exists(_executionLogPath)) return;

				var lines = File.ReadAllLines(_executionLogPath);
				var now = DateTimeOffset.UtcNow;

				lock (_cacheLock)
				{
					foreach (var line in lines)
					{
						if (DateTimeOffset.TryParse(line, out var timestamp) &&
							(now - timestamp).TotalDays < 40)
						{
							_executionCache.Add(timestamp);
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Fejl ved indlæsning af kørselshistorik");
			}
		}

		private void SyncExecutionCacheToFile()
		{
			try
			{
				var now = DateTimeOffset.UtcNow;
				List<string> recentExecutions;

				lock (_cacheLock)
				{
					recentExecutions = _executionCache
						.Where(ts => (now - ts).TotalDays < 40)
						.OrderBy(ts => ts)
						.Select(ts => ts.ToString("O"))
						.ToList();
				}

				if (recentExecutions.Count != 0)
					File.WriteAllLines(_executionLogPath, recentExecutions);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Fejl ved synkronisering af kørselslog");
			}
		}
	}
}
