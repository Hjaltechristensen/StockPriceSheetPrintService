using Serilog;
using StockPriceSheetPrintService.DiscordUpdates;
using StockPrizeSenderService;
using StockPrizeSenderService.GoogleSheets;

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

builder.Services.AddHostedService<StockprizeWorker>();
builder.Services.AddSingleton<StockprizeWorker>();
builder.Services.AddSingleton<HtmlScraper>();
builder.Services.AddSingleton<UpdateCellAsync>();
builder.Services.AddHttpClient<DiscordNotifier>();

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
