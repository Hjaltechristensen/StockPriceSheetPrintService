using Microsoft.AspNetCore.Mvc;
using Serilog.Context;
using StockPriceSheetPrintService.Service;
using StockPriceSheetPrintService.Service.Ports.Inbound;

namespace StockPriceSheetPrintService.Inbound.Controllers
{
	[ApiController]
	[Route("saxo")]
	public class SaxoAuthController : ControllerBase
	{
		private readonly ISaxoLoginService _saxoLoginService;
		private readonly ISaxoManagementService _saxoManagementService;
		private readonly IPortfolioJobRunner _jobRunner;
		private readonly ILogger<SaxoAuthController> _logger;

		public SaxoAuthController(ISaxoLoginService saxoLoginService, ISaxoManagementService saxoManagementService, IPortfolioJobRunner jobRunner, ILogger<SaxoAuthController> logger)
		{
			_saxoLoginService = saxoLoginService;
			_saxoManagementService = saxoManagementService;
			_jobRunner = jobRunner;
			_logger = logger;
		}

		[HttpGet("login")]
		public async Task<IActionResult> GetLoginUrl(CancellationToken ct)
		{
			var ctx = ClientContextFactory.New("HTTP:login");
			using var _1 = LogContext.PushProperty("CorrelationId", ctx.CorrelationId);
			using var _2 = LogContext.PushProperty("Source", ctx.Source);
			var url = await _saxoLoginService.GetLoginUrlAsync(ctx, ct);
			return Content(url);
		}

		[HttpGet("callback")]
		public async Task<IActionResult> Callback([FromQuery] string code, CancellationToken ct)
		{
			var ctx = ClientContextFactory.New("HTTP:callback");
			using var _1 = LogContext.PushProperty("CorrelationId", ctx.CorrelationId);
			using var _2 = LogContext.PushProperty("Source", ctx.Source);

			_logger.LogInformation("[SAXO-CALLBACK] OAuth callback starter");

			if (string.IsNullOrEmpty(code))
				return BadRequest("No code received from Saxo.");

			try
			{
				var result = await _saxoManagementService.HandleCallbackAsync(code, ctx, ct);
				return Ok(new
				{
					Message = "Everything is set up! Your worker will now run automatically.",
					Værdi = result.TotalValue,
					Valuta = result.Currency,
					NextRunTime = "Check logs for next scheduled run"
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[SAXO-CALLBACK] Error in callback");
				return StatusCode(500, $"Internal server error: {ex.Message}");
			}
		}

		[HttpPost("trigger")]
		public async Task<IActionResult> TriggerJob(CancellationToken ct)
		{
			var ctx = ClientContextFactory.New("HTTP:trigger");
			using var _1 = LogContext.PushProperty("CorrelationId", ctx.CorrelationId);
			using var _2 = LogContext.PushProperty("Source", ctx.Source);
			await _jobRunner.RunJobAsync(ctx, ct, true);
			return Ok(new { Message = "Job completed." });
		}

		[HttpPost("refreshToken")]
		public async Task<IActionResult> RefreshSaxoAccessTokenAsync(CancellationToken ct)
		{
			var ctx = ClientContextFactory.New("HTTP:refreshToken");
			using var _1 = LogContext.PushProperty("CorrelationId", ctx.CorrelationId);
			using var _2 = LogContext.PushProperty("Source", ctx.Source);
			var accessToken = await _saxoManagementService.GetOrRefreshAccessTokenAsync(ctx, ct);
			if (accessToken == null) return NotFound(new { Message = "No valid access token found. Log in via /saxo/login" });
			return Ok(new { Message = "Token refresh completed." });
		}

		[HttpPost("getAccessToken")]
		public async Task<IActionResult> GetAccessTokenAsync(CancellationToken ct)
		{
			var ctx = ClientContextFactory.New("HTTP:getAccessToken");
			using var _1 = LogContext.PushProperty("CorrelationId", ctx.CorrelationId);
			using var _2 = LogContext.PushProperty("Source", ctx.Source);
			var accessToken = await _saxoManagementService.GetOrRefreshAccessTokenAsync(ctx, ct);
			if (accessToken == null)
				return NotFound(new { Message = "No valid access token found. Log in via /saxo/login" });
			return Ok(new { AccessToken = accessToken });
		}
	}
}
