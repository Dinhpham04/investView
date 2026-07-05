using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InvestView.Api.Tests.MarketData;

public sealed class MarketQuotesEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MarketQuotesEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMarketQuotes_ReturnsMockMarketBoardShape()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/market/quotes");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var quotes = payload.RootElement;
        Assert.Equal(JsonValueKind.Array, quotes.ValueKind);
        Assert.Equal(3, quotes.GetArrayLength());

        var firstQuote = quotes[0];
        Assert.Equal("HPG", firstQuote.GetProperty("symbol").GetString());
        Assert.Equal("G1", firstQuote.GetProperty("boardId").GetString());
        Assert.Equal(29150m, firstQuote.GetProperty("lastPrice").GetDecimal());
        Assert.Equal(550m, firstQuote.GetProperty("change").GetDecimal());
        Assert.Equal(1.92m, firstQuote.GetProperty("changePercent").GetDecimal());
        Assert.Equal("Continuous", firstQuote.GetProperty("tradingStatus").GetString());
        Assert.Equal("2026-07-03T07:45:00+00:00", firstQuote.GetProperty("updatedAt").GetString());

        var bidLevels = firstQuote.GetProperty("bidLevels");
        Assert.Equal(3, bidLevels.GetArrayLength());
        Assert.Equal(29100m, bidLevels[0].GetProperty("price").GetDecimal());
        Assert.Equal(18300, bidLevels[0].GetProperty("quantity").GetInt64());

        var askLevels = firstQuote.GetProperty("askLevels");
        Assert.Equal(3, askLevels.GetArrayLength());
        Assert.Equal(29150m, askLevels[0].GetProperty("price").GetDecimal());
        Assert.Equal(12000, askLevels[0].GetProperty("quantity").GetInt64());
    }

    [Fact]
    public async Task GetMarketQuotes_WhenSymbolsAreProvided_ReturnsFilteredQuotes()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/market/quotes?symbols=SSI&boardId=G1");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, payload.RootElement.GetArrayLength());
        Assert.Equal("SSI", payload.RootElement[0].GetProperty("symbol").GetString());
    }
}
