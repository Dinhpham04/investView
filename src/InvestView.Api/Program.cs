using InvestView.Api.Hubs;
using InvestView.Application.Abstractions.Realtime;
using InvestView.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

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

app.UseAuthorization();

app.MapControllers();
app.MapHub<QuoteHub>("/hubs/quotes");

app.Run();

public partial class Program;
