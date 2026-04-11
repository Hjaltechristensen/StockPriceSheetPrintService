using Microsoft.AspNetCore.Mvc;
using StockPriceSheetPrintService.Service.Ports;
using StockPriceSheetPrintService.Service.Ports.Inbound;

namespace StockPriceSheetPrintService.Inbound.Controllers
{
	[ApiController]
	[Route("saxo")]
	public class SaxoAuthController : ControllerBase
	{
		private readonly ISaxoService _saxoAuthService;
		private readonly ITokenStore _tokenStore;
		private readonly ISaxoTokenService _saxoTokenService;
		private readonly IPortfolioJobRunner _jobRunner;
		private readonly ILogger<SaxoAuthController> _logger;

		public SaxoAuthController(ISaxoService saxoAuthService, ITokenStore tokenStore, ISaxoTokenService saxoTokenService, IPortfolioJobRunner jobRunner, ILogger<SaxoAuthController> logger)
		{
			_saxoAuthService = saxoAuthService;
			_tokenStore = tokenStore;
			_saxoTokenService = saxoTokenService;
			_jobRunner = jobRunner;
			_logger = logger;
		}

		[HttpGet("login")]
		public async Task<IActionResult> GetLoginUrl()
		{
			var url = await _saxoAuthService.BuildLoginUrl();
			return Content(url);
		}

		[HttpGet("callback")]
		public async Task<IActionResult> Callback([FromQuery] string code, CancellationToken ct)
		{
			_logger.LogInformation("[SAXO-CALLBACK] OAuth callback starter");

			if (string.IsNullOrEmpty(code))
				return BadRequest("Ingen kode modtaget fra Saxo.");

			try
			{
				var tokens = await _saxoAuthService.ExchangeCodeForTokensAsync(code, ct);
				await _tokenStore.SaveRefreshTokenAsync(tokens.RefreshToken, ct);
				var balance = await _saxoAuthService.GetBalanceAsync(tokens.AccessToken, ct);

				return Ok(new
				{
					Message = "Alt er sat op! Din worker vil nu køre automatisk.",
					Værdi = balance.TotalValue,
					Valuta = balance.Currency,
					NextRunTime = "Check logs for næste planlagte kørsel"
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[SAXO-CALLBACK] Fejl i callback");
				return StatusCode(500, $"Intern serverfejl: {ex.Message}");
			}
		}

		[HttpPost("trigger")]
		public async Task<IActionResult> TriggerJob(CancellationToken ct)
		{
			await _jobRunner.RunJobAsync(ct, true);
			return Ok(new { Message = "Kørsel gennemført." });
		}

		[HttpPost("refreshToken")]
		public async Task<IActionResult> RefreshSaxoAccessTokenAsync(CancellationToken ct)
		{
			await _saxoTokenService.GetAccessTokenAsync(ct);
			return Ok(new { Message = "Token refresh gennemført." });
		}

		[HttpPost("getAccessToken")]
		public async Task<IActionResult> GetAccessTokenAsync(CancellationToken ct)
		{
			var accessToken = await _saxoTokenService.GetAccessTokenAsync(ct);
			if (accessToken == null)
				return NotFound(new { Message = "Ingen gyldig access token fundet. Log ind via /saxo/login" });
			return Ok(new { AccessToken = accessToken });
		}
	}
}
