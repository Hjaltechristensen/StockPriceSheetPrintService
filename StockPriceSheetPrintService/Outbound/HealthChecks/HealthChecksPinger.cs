using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService.Outbound.HealthChecks
{
	public class HealthChecksPinger : IHealthCheckPinger
	{
		private readonly HttpClient _httpClient;
		private readonly ILogger<HealthChecksPinger> _logger;
		private readonly string _pingUrl;

		public HealthChecksPinger(HttpClient httpClient, IConfiguration configuration, ILogger<HealthChecksPinger> logger)
		{
			_httpClient = httpClient;
			_logger = logger;
			_pingUrl = configuration["HealthChecks:PingUrl"] ?? string.Empty;

			if (string.IsNullOrEmpty(_pingUrl))
				_logger.LogWarning("[HEALTHCHECKS] HealthChecks:PingUrl configuration missing");
		}

		public Task PingSuccessAsync(CancellationToken ct) => SendPingAsync(_pingUrl, ct);

		public Task PingFailureAsync(CancellationToken ct) => SendPingAsync($"{_pingUrl}/fail", ct);

		private async Task SendPingAsync(string url, CancellationToken ct)
		{
			if (string.IsNullOrWhiteSpace(_pingUrl))
				return;

			try
			{
				await _httpClient.GetAsync(url, ct);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "[HEALTHCHECKS] Failed to reach healthchecks.io");
			}
		}
	}
}
