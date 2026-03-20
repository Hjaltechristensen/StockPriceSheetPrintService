namespace StockPriceSheetPrintService.Service.Ports
{
	public interface IExecutionGuard
	{
		bool IsExecutionSafe();
		void LogExecution();
	}
}
