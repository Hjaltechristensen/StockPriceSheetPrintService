namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IHealthCheckPinger
	{
		Task PingSuccessAsync(CancellationToken ct);
		Task PingFailureAsync(CancellationToken ct);
	}
}
