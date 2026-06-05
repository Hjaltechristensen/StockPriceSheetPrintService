using Microsoft.AspNetCore.Mvc;
using StockPriceSheetPrintService.Service.Ports.Inbound;

namespace StockPriceSheetPrintService.Inbound.Controllers
{
    [ApiController]
    [Route("dashboard")]
    public class DashboardController(IDashboardService dashboardService) : ControllerBase
    {
        [HttpGet("data")]
        public async Task<IActionResult> GetData(CancellationToken ct)
        {
            var entries = await dashboardService.GetHistoricalDataAsync(ct);
            var payload = entries.Select(e => new
            {
                date = e.Date.ToString("yyyy-MM-dd"),
                value = e.Value
            });
            return Ok(payload);
        }
    }
}
