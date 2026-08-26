using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace StockPriceSheetPrintService.Inbound.Filters
{
	public class AdminApiKeyFilterAttribute : ActionFilterAttribute
	{
		private const string HeaderName = "X-Admin-Key";

		public override void OnActionExecuting(ActionExecutingContext context)
		{
			var configuredKey = context.HttpContext.RequestServices
				.GetRequiredService<IConfiguration>()["Admin:ApiKey"];

			if (string.IsNullOrEmpty(configuredKey) ||
				!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var providedKey) ||
				!FixedTimeEquals(configuredKey, providedKey.ToString()))
			{
				context.Result = new UnauthorizedResult();
			}
		}

		private static bool FixedTimeEquals(string a, string b) =>
			CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
	}
}
