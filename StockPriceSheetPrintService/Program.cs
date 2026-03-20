using Serilog;
using StockPriceSheetPrintService.Outbound.DiscordUpdates;
using StockPriceSheetPrintService.Outbound.Filesystem;
using StockPriceSheetPrintService.Outbound.GoogleSheets;
using StockPriceSheetPrintService.Outbound.Saxo;
using StockPriceSheetPrintService.Service;
using StockPriceSheetPrintService.Service.Application;
using StockPriceSheetPrintService.Service.Helpers;
using StockPriceSheetPrintService.Service.Ports;

Log.Logger = new LoggerConfiguration()
	.Enrich.FromLogContext()
	.WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter())
	.CreateLogger();


var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Host.UseSerilog();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddHostedService<StockpriceWorker>();
builder.Services.AddHttpClient<IDiscordNotifier, DiscordNotifier>();
builder.Services.AddScoped<PortfolioCalculator>();
builder.Services.AddScoped<IPortfolioJobRunner, PortfolioJobRunner>();
builder.Services.AddScoped<ITokenStore, EncryptedFileTokenStore>();
builder.Services.AddScoped<ISaxoAuthService, SaxoAuthService>();
builder.Services.AddScoped<ISaxoTokenService, SaxoTokenService>(); 
builder.Services.AddSingleton<IFundPriceProvider, HtmlScraper>();
builder.Services.AddSingleton<IGoogleSheetsClient, UpdateCellAsync>();
builder.Services.AddSingleton<IExecutionGuard, ExecutionGuard>();

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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
