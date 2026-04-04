using Microsoft.AspNetCore.Mvc;
using StockPriceSheetPrintService.Service.Ports;

namespace StockPriceSheetPrintService.Inbound.Controllers
{
    [ApiController]
    [Route("dashboard")]
    public class DashboardController(IGoogleSheetsClient googleSheetsClient, IConfiguration configuration) : ControllerBase
    {
        [HttpGet("data")]
        public async Task<IActionResult> GetData(CancellationToken ct)
        {
            var spreadsheetId = configuration["SheetsApi:SheetsKey"]
                ?? throw new InvalidOperationException("SheetsApi:SheetsKey er ikke konfigureret");

            var entries = await googleSheetsClient.GetHistoricalDataAsync(spreadsheetId, "Daily", ct);

            var payload = entries.Select(e => new
            {
                date = e.Date.ToString("yyyy-MM-dd"),
                value = e.Value
            });

            return Ok(payload);
        }
    }
}
