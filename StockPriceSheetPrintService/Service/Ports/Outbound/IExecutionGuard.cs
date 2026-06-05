namespace StockPriceSheetPrintService.Service.Ports.Outbound
{
	public interface IExecutionGuard
	{
		bool IsExecutionSafe();
		void LogExecution();
	}
}
