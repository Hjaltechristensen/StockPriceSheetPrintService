using StockPrizeSenderService;
using StockPrizeSenderService.GoogleSheets;
using StockPrizeSenderService.Models;
using StockPrizeSenderService.TestData;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddHostedService<StockprizeWorker>();
builder.Services.AddSingleton<StockprizeWorker>();
builder.Services.AddSingleton<HtmlScraper>();
builder.Services.AddSingleton<UpdateCellAsync>();
builder.Services.AddSingleton<TestDataClass>();

builder.Services.AddHttpClient("StockApi", client =>
{
	client.BaseAddress = new Uri("https://api.marketstack.com/");
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
