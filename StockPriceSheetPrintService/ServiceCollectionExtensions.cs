using StockPriceSheetPrintService.Outbound.DiscordUpdates;
using StockPriceSheetPrintService.Outbound.GeminiInsights;
using StockPriceSheetPrintService.Outbound.GoogleSheets;
using StockPriceSheetPrintService.Outbound.HealthChecks;
using StockPriceSheetPrintService.Outbound.HtmlScraping;
using StockPriceSheetPrintService.Outbound.MarketStack;
using StockPriceSheetPrintService.Outbound.Memory;
using StockPriceSheetPrintService.Outbound.Persistence;
using StockPriceSheetPrintService.Outbound.Saxo;
using StockPriceSheetPrintService.Service.Application;
using StockPriceSheetPrintService.Service.Ports.Inbound;
using StockPriceSheetPrintService.Service.Ports.Outbound;

namespace StockPriceSheetPrintService
{
	public static class ServiceCollectionExtensions
	{
		public static IServiceCollection AddInboundServices(this IServiceCollection services)
		{
			services.AddScoped<IDashboardService, DashboardServiceImpl>();
			services.AddSingleton<IDiscordBotMessageReceiver, DiscordMessageDistributor>();
			services.AddScoped<IPortfolioJobRunner, PortfolioJobRunner>();
			services.AddScoped<ISaxoLoginService, SaxoLoginServiceImpl>();
			services.AddScoped<ISaxoManagementService, SaxoManagementServiceImpl>();
			services.AddScoped<ITriggerReportService, TriggerReportServiceImpl>();

			return services;
		}

		public static IServiceCollection AddOutboundServices(this IServiceCollection services)
		{
			services.AddHttpClient<IDiscordNotifier, DiscordNotifier>();
			services.AddSingleton<IExecutionGuard, DbExecutionGuard>();
			services.AddScoped<IGeminiReportInsights, GeminiReportInsightsImpl>();
			services.AddSingleton<IGeminiToggle, DbGeminiToggleStore>();
			services.AddSingleton<IGoogleSheetsClient, GoogleSheetsClientImpl>();
			services.AddHttpClient<IHealthCheckPinger, HealthChecksPinger>();
			services.AddHttpClient<IHtmlScraper, NavProviderImpl>(client =>
			{
				client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
				client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
				client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
			});
			services.AddSingleton<IJuneStore, DbJuneStore>();
			services.AddScoped<IMarketStackService, MarketStackService>();
			services.AddSingleton<INordnetStore, DbNordnetStore>();
			services.AddSingleton<INordnetSymbolStore, DbNordnetSymbolStore>();
			services.AddSingleton<IPendingReportStore, InMemoryPendingReportStore>();
			services.AddScoped<IPortfolioCalculator, PortfolioCalculator>();
			services.AddScoped<IPortfolioDataFetcher, PortfolioDataFetcher>();
			services.AddScoped<IPortfolioReporter, PortfolioReporter>();
			services.AddScoped<ISaxoAccountService, SaxoService>();
			services.AddScoped<ISaxoAuthService, SaxoService>();
			services.AddScoped<ISaxoNetPositionStore, DbSaxoNetPositions>();
			services.AddScoped<ISaxoTokenService, SaxoTokenService>();
			services.AddSingleton<ISchedulerStatus>(sp => sp.GetRequiredService<SchedulerStatusStore>());
			services.AddScoped<ISeenTransferStore, DbSeenTransferStore>();
			services.AddScoped<ITokenStore, DbTokenStore>();

			return services;
		}
	}
}
