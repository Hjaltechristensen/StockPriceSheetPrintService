using Serilog;
using Serilog.Events;
using StockPriceSheetPrintService.Outbound.DiscordUpdates;
using StockPriceSheetPrintService.Outbound.Filesystem;
using StockPriceSheetPrintService.Outbound.GoogleSheets;
using StockPriceSheetPrintService.Outbound.Saxo;
using StockPriceSheetPrintService.Service;
using StockPriceSheetPrintService.Service.Application;
using StockPriceSheetPrintService.Service.Helpers;
using StockPriceSheetPrintService.Service.Ports;

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
builder.Services.AddHttpClient<IDiscordNotifier, DiscordNotifier>();
builder.Services.AddScoped<PortfolioCalculator>();
builder.Services.AddScoped<IPortfolioJobRunner, PortfolioJobRunner>();
builder.Services.AddScoped<ITokenStore, EncryptedFileTokenStore>();
builder.Services.AddScoped<ISaxoAuthService, SaxoAuthService>();
builder.Services.AddScoped<ISaxoTokenService, SaxoTokenService>(); 
builder.Services.AddSingleton<IGoogleSheetsClient, UpdateCellAsync>();
builder.Services.AddSingleton<IExecutionGuard, ExecutionGuard>();
builder.Services.AddScoped<IFundPriceProvider, HtmlScraper>();
builder.Services.AddHttpClient<FundNavClient>(client =>
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

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
