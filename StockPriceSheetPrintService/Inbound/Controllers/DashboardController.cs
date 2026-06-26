using Microsoft.AspNetCore.Mvc;
using Serilog.Context;
using StockPriceSheetPrintService.Service;
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
            var ctx = ClientContextFactory.New("HTTP:dashboard");
            using var _1 = LogContext.PushProperty("CorrelationId", ctx.CorrelationId);
            using var _2 = LogContext.PushProperty("Source", ctx.Source);
            var entries = await dashboardService.GetHistoricalDataAsync(ctx, ct);
            var payload = entries.Select(e => new
            {
                date = e.Date.ToString("yyyy-MM-dd"),
                value = e.Value
            });
            return Ok(payload);
        }
    }
}
