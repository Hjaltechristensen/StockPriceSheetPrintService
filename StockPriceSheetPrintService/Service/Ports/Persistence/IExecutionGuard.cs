namespace StockPriceSheetPrintService.Service.Ports.Persistence
{
	public interface IExecutionGuard
	{
		bool IsExecutionSafe();
		void LogExecution();
	}
}
