using Serilog;
using Serilog.Events;
using StockPriceSheetPrintService.Inbound.Listener;
using StockPriceSheetPrintService.Outbound.DiscordUpdates;
using StockPriceSheetPrintService.Outbound.Filesystem;
using StockPriceSheetPrintService.Outbound.GoogleSheets;
using StockPriceSheetPrintService.Outbound.HtmlScraping;
using StockPriceSheetPrintService.Outbound.MarketStack;
using StockPriceSheetPrintService.Outbound.Persistence;
using StockPriceSheetPrintService.Outbound.Saxo;
using StockPriceSheetPrintService.Service;
using StockPriceSheetPrintService.Service.Application;
using StockPriceSheetPrintService.Service.Ports.Inbound;
using StockPriceSheetPrintService.Service.Ports.Outbound;
using StockPriceSheetPrintService.Service.Ports.Persistence;

var errorWebhook = Environment.GetEnvironmentVariable("Discord__WebhookError")
	?? throw new InvalidOperationException("Discord:WebhookError missing");

Log.Logger = new LoggerConfiguration()
	.Enrich.FromLogContext()
	.WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter())
	.WriteTo.Sink(new DiscordSink(errorWebhook), LogEventLevel.Warning)
	.CreateLogger();


var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddHostedService<StockpriceWorker>();
builder.Services.AddHostedService<DiscordBotListener>();
builder.Services.AddHttpClient<IDiscordNotifier, DiscordNotifier>();
builder.Services.AddScoped<IPortfolioCalculator, PortfolioCalculator>();
builder.Services.AddScoped<IPortfolioJobRunner, PortfolioJobRunner>();
builder.Services.AddScoped<ISaxoLoginService, SaxoLoginServiceImpl>();
builder.Services.AddScoped<IPortfolioDataFetcher, PortfolioDataFetcher>();
builder.Services.AddScoped<IPortfolioReporter, PortfolioReporter>();
builder.Services.AddScoped<ITokenStore, EncryptedFileTokenStore>();
builder.Services.AddScoped<ISeenTransferStore, SeenTransferStore>();
builder.Services.AddScoped<ISaxoAuthService, SaxoService>();
builder.Services.AddScoped<ISaxoAccountService, SaxoService>();
builder.Services.AddScoped<ISaxoTokenService, SaxoTokenService>();
builder.Services.AddSingleton<IGoogleSheetsClient, GoogleSheetsClientImpl>();
builder.Services.AddSingleton<IExecutionGuard, ExecutionGuardImpl>();
builder.Services.AddScoped<IMarketStackService, MarketStackService>();
builder.Services.AddSingleton<INordnetStore, JsonNordnetStore>();
builder.Services.AddSingleton<IJuneStore, JsonJuneStore>();
builder.Services.AddSingleton<IDiscordBotMessageReceiver, DiscordMessageDistributor>();
builder.Services.AddHttpClient<IHtmlScraper, NavProviderImpl>(client =>
{
	client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
	client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
	client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
});

builder.Services.AddHttpClient("StockApi", client =>
{
	client.BaseAddress = new Uri("https://api.marketstack.com/");
});

builder.Services.AddHttpClient("NationalbankApi", c =>
{
	c.BaseAddress = new Uri("https://www.nationalbanken.dk/");
});


builder.WebHost.ConfigureKestrel(options =>
{
	options.ListenAnyIP(5151);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();
app.Run();
