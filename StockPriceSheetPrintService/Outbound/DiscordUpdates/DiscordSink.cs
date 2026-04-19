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

			var embedList = new List<object>
			{
				new
				{
					title = level,
					description = message + exception,
					color = logEvent.Level == LogEventLevel.Warning ? 16776960 : 15158332,
					timestamp = logEvent.Timestamp.UtcDateTime
				}
			};

			if (message.Contains("Overriding address(es)") && message.Contains("Binding to endpoints defined via IConfiguration and/or UseKestrel() instead"))
			{
				embedList.Insert(0,new
				{
					title = "✅ Startup",
					description = "Startup completed...",
					color = 3066993,
					timestamp = logEvent.Timestamp.UtcDateTime
				});
			}

			var payload = new { embeds = embedList.ToArray() };

			Task.Run(async () =>
			{
				await _httpClient.PostAsJsonAsync(_webhookUrl, payload);
			});
		}
	}
}
