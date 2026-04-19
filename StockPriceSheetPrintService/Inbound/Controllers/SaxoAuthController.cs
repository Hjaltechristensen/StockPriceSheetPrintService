using Microsoft.AspNetCore.Mvc;
using StockPriceSheetPrintService.Service.Ports.Inbound;
using StockPriceSheetPrintService.Service.Ports.Outbound;
using StockPriceSheetPrintService.Service.Ports.Persistence;

namespace StockPriceSheetPrintService.Inbound.Controllers
{
	[ApiController]
	[Route("saxo")]
	public class SaxoAuthController : ControllerBase
	{
		private readonly ISaxoLoginService _saxoLoginService;
		private readonly ISaxoAuthService _saxoAuthService;
		private readonly ISaxoAccountService _saxoAccountService;
		private readonly ITokenStore _tokenStore;
		private readonly ISaxoTokenService _saxoTokenService;
		private readonly IPortfolioJobRunner _jobRunner;
		private readonly ILogger<SaxoAuthController> _logger;

		public SaxoAuthController(ISaxoLoginService saxoLoginService, ISaxoAuthService saxoAuthService, ISaxoAccountService saxoAccountService, ITokenStore tokenStore, ISaxoTokenService saxoTokenService, IPortfolioJobRunner jobRunner, ILogger<SaxoAuthController> logger)
		{
			_saxoLoginService = saxoLoginService;
			_saxoAuthService = saxoAuthService;
			_saxoAccountService = saxoAccountService;
			_tokenStore = tokenStore;
			_saxoTokenService = saxoTokenService;
			_jobRunner = jobRunner;
			_logger = logger;
		}

		[HttpGet("login")]
		public async Task<IActionResult> GetLoginUrl(CancellationToken ct)
		{
			var url = await _saxoLoginService.GetLoginUrlAsync(ct);
			return Content(url);
		}

		[HttpGet("callback")]
		public async Task<IActionResult> Callback([FromQuery] string code, CancellationToken ct)
		{
			_logger.LogInformation("[SAXO-CALLBACK] OAuth callback starter");

			if (string.IsNullOrEmpty(code))
				return BadRequest("No code received from Saxo.");

			try
			{
				var tokens = await _saxoAuthService.ExchangeCodeForTokensAsync(code, ct);
				await _tokenStore.SaveRefreshTokenAsync(tokens.RefreshToken, ct);
				var balance = await _saxoAccountService.GetBalanceAsync(tokens.AccessToken, ct);

				return Ok(new
				{
					Message = "Everything is set up! Your worker will now run automatically.",
					Værdi = balance?.TotalValue,
					Valuta = balance?.Currency,
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
			await _jobRunner.RunJobAsync(ct, true);
			return Ok(new { Message = "Job completed." });
		}

		[HttpPost("refreshToken")]
		public async Task<IActionResult> RefreshSaxoAccessTokenAsync(CancellationToken ct)
		{
			var accessToken = await _saxoTokenService.GetAccessTokenAsync(ct);
			if (accessToken == null) return NotFound(new { Message = "No valid access token found. Log in via /saxo/login" });
			return Ok(new { Message = "Token refresh completed." });
		}

		[HttpPost("getAccessToken")]
		public async Task<IActionResult> GetAccessTokenAsync(CancellationToken ct)
		{
			var accessToken = await _saxoTokenService.GetAccessTokenAsync(ct);
			if (accessToken == null)
				return NotFound(new { Message = "No valid access token found. Log in via /saxo/login" });
			return Ok(new { AccessToken = accessToken });
		}
	}
}
