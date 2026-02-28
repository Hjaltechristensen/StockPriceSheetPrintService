using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace StockPriceSheetPrintService.Logging
{
	public class CleanFormatter : ConsoleFormatter
	{
		public CleanFormatter() : base("clean") { }

		public override void Write<TState>(
			in LogEntry<TState> logEntry,
			IExternalScopeProvider? scopeProvider,
			TextWriter textWriter)
		{
			var level = logEntry.LogLevel switch
			{
				LogLevel.Information => "info",
				LogLevel.Warning => "warn",
				LogLevel.Error => "fail",
				LogLevel.Critical => "crit",
				LogLevel.Debug => "dbug",
				LogLevel.Trace => "trce",
				_ => "    "
			};

			textWriter.WriteLine($"{level}: {logEntry.Formatter(logEntry.State, logEntry.Exception)}");
		}
	}
}
