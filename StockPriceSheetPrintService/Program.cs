using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using StockPriceSheetPrintService;
using StockPriceSheetPrintService.Inbound.Listener;
using StockPriceSheetPrintService.Outbound.DiscordUpdates;
using StockPriceSheetPrintService.Outbound.Persistence;
using StockPriceSheetPrintService.Service.Application;

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

builder.Services.AddDbContextFactory<StockDbContext>(options =>
	options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")
		?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection missing")));

builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
	GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
}));
builder.Services.AddSingleton<SchedulerStatusStore>();
builder.Services.AddHostedService<StockpriceWorker>();
builder.Services.AddHostedService<DiscordBotListener>();

builder.Services.AddInboundServices();
builder.Services.AddOutboundServices();


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

using (var scope = app.Services.CreateScope())
{
	var db = await scope.ServiceProvider
		.GetRequiredService<IDbContextFactory<StockDbContext>>()
		.CreateDbContextAsync();
	await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();
await app.RunAsync();
