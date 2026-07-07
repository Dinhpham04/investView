using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace InvestView.Api.Tests.Realtime;

public sealed class QuoteHubEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public QuoteHubEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MarketData:Provider"] = "Mock",
                    ["MarketData:QuoteStream:Enabled"] = "false"
                })));
    }

    [Fact]
    public async Task QuoteHub_Negotiate_ReturnsSignalRConnectionMetadata()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync("/hubs/quotes/negotiate?negotiateVersion=1", content: null);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.RootElement.TryGetProperty("connectionId", out var connectionId));
        Assert.False(string.IsNullOrWhiteSpace(connectionId.GetString()));
        Assert.True(payload.RootElement.TryGetProperty("availableTransports", out var transports));
        Assert.NotEmpty(transports.EnumerateArray());
    }
}
