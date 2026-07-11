using InvestView.Api.Auth;
using InvestView.Api.Hubs;
using InvestView.Application.Abstractions.Realtime;
using InvestView.Infrastructure;
using InvestView.Infrastructure.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInvestViewJwtAuthentication(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IMarketQuoteBroadcaster, SignalRMarketQuoteBroadcaster>();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<QuoteHub>("/hubs/quotes");

await app.Services.SeedDemoDataAsync(app.Lifetime.ApplicationStopping);

app.Run();

public partial class Program;
