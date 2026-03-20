using Serilog.Core;
using Serilog.Events;

namespace StockPriceSheetPrintService.Outbound.DiscordUpdates
{
	public class DiscordSink : ILogEventSink
	{
		private readonly HttpClient _httpClient = new();
		private readonly string _webhookUrl;

		public DiscordSink(string webhookUrl)
		{
			_webhookUrl = webhookUrl;
		}

		public void Emit(LogEvent logEvent)
		{
			var level = logEvent.Level switch
			{
				LogEventLevel.Warning => "⚠️ Warning",
				LogEventLevel.Error => "❌ Error",
				LogEventLevel.Fatal => "💀 Fatal",
				_ => logEvent.Level.ToString()
			};

			var message = logEvent.RenderMessage();
			var exception = logEvent.Exception != null
				? $"\n```{logEvent.Exception.Message}```"
				: string.Empty;

			var payload = new
			{
				embeds = new[]
				{
				new
				{
					title = level,
					description = message + exception,
					color = logEvent.Level == LogEventLevel.Warning ? 16776960 : 15158332,
					timestamp = logEvent.Timestamp.UtcDateTime
				}
			}
			};

			Task.Run(async () =>
			{
				await _httpClient.PostAsJsonAsync(_webhookUrl, payload);
			});
		}
	}
}
