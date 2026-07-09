using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace InvestView.Api.Tests.MarketData;

public sealed class MarketQuotesEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MarketQuotesEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder
            .UseInMemoryMarketStateForTests()
            .ConfigureAppConfiguration((_, configurationBuilder) =>
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MarketData:Provider"] = "Mock"
                })));
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
        Assert.Equal(786100, firstQuote.GetProperty("foreignBuyVolume").GetInt64());
        Assert.Equal(1227649, firstQuote.GetProperty("foreignSellVolume").GetInt64());
        Assert.Equal(1742502798, firstQuote.GetProperty("foreignRoom").GetInt64());
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

    [Fact]
    public async Task GetMarketQuotes_WhenSymbolsAreCommaSeparated_ReturnsFilteredQuotes()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/market/quotes?symbols=HPG,SSI&boardId=G1");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["HPG", "SSI"], payload.RootElement.EnumerateArray().Select(quote => quote.GetProperty("symbol").GetString()));
    }

    [Fact]
    public async Task GetMarketQuotes_WhenMarketIdIsProvided_ReturnsExchangeQuotes()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/market/quotes?marketId=STO&boardId=G1");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, payload.RootElement.GetArrayLength());
        Assert.All(payload.RootElement.EnumerateArray(), quote =>
            Assert.Equal("HOSE", quote.GetProperty("marketId").GetString()));
    }

    [Fact]
    public async Task GetMarketQuotes_WhenIndexNameIsProvided_ReturnsIndexQuotes()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/market/quotes?indexName=VN30&boardId=G1");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["HPG", "SSI", "VCB"], payload.RootElement.EnumerateArray().Select(quote => quote.GetProperty("symbol").GetString()));
    }

    [Fact]
    public async Task GetSymbolDetail_ReturnsMockSymbolSnapshotAndMetadata()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/market/symbols/HPG?boardId=G1");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("HPG", payload.RootElement.GetProperty("symbol").GetString());
        Assert.Equal("G1", payload.RootElement.GetProperty("boardId").GetString());
        Assert.Equal("VN000000HPG", payload.RootElement.GetProperty("isin").GetString());
        Assert.Equal("STOCK", payload.RootElement.GetProperty("productGroupId").GetString());
        Assert.Equal("ST", payload.RootElement.GetProperty("securityGroupId").GetString());
        Assert.Equal(29150m, payload.RootElement.GetProperty("lastPrice").GetDecimal());
        Assert.Equal(550m, payload.RootElement.GetProperty("change").GetDecimal());
        Assert.Equal(786100, payload.RootElement.GetProperty("foreignBuyVolume").GetInt64());
        Assert.Equal(3, payload.RootElement.GetProperty("bidLevels").GetArrayLength());
        Assert.Equal("NORMAL", payload.RootElement.GetProperty("symbolAdminStatus").GetString());
    }

    [Fact]
    public async Task GetOhlc_ReturnsMockBarsForSymbol()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/market/symbols/HPG/ohlc?resolution=1");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, payload.RootElement.GetArrayLength());
        Assert.Equal("HPG", payload.RootElement[0].GetProperty("symbol").GetString());
        Assert.Equal("1", payload.RootElement[0].GetProperty("resolution").GetString());
        Assert.True(payload.RootElement[0].GetProperty("volume").GetInt64() > 0);
    }

    [Fact]
    public async Task GetMarketIndices_ReturnsMockIndexOverview()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/market/indices?names=VNINDEX&names=VN30");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, payload.RootElement.GetArrayLength());
        Assert.Equal(["VN30", "VNINDEX"], payload.RootElement.EnumerateArray().Select(index => index.GetProperty("indexName").GetString()));
        Assert.True(payload.RootElement[0].GetProperty("value").GetDecimal() > 0m);
        Assert.True(payload.RootElement[0].GetProperty("totalVolume").GetInt64() > 0);
        Assert.True(payload.RootElement[0].GetProperty("upCount").GetInt32() >= 0);
    }

    [Fact]
    public async Task GetIndexOhlc_ReturnsMockBarsForIndex()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/market/indices/VNINDEX/ohlc?resolution=1");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.RootElement.GetArrayLength() > 0);
        Assert.Equal("VNINDEX", payload.RootElement[0].GetProperty("symbol").GetString());
        Assert.Equal("1", payload.RootElement[0].GetProperty("resolution").GetString());
        Assert.True(payload.RootElement[0].GetProperty("close").GetDecimal() > 0m);
    }

    [Fact]
    public async Task GetLatestTrades_ReturnsMockTradesForSymbol()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/market/symbols/HPG/trades/latest?boardId=G1&limit=2");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, payload.RootElement.GetArrayLength());
        Assert.Equal("HPG", payload.RootElement[0].GetProperty("symbol").GetString());
        Assert.Equal("G1", payload.RootElement[0].GetProperty("boardId").GetString());
        Assert.True(payload.RootElement[0].GetProperty("price").GetDecimal() > 0m);
        Assert.True(payload.RootElement[0].GetProperty("quantity").GetInt64() > 0);
    }
}
