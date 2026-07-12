using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace InvestView.Api.Tests.Trading;

public sealed class SimulatedTradingEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string DemoEmail = "demo@investview.local";
    private const string DemoPassword = "demo-password";

    private readonly WebApplicationFactory<Program> _factory;

    public SimulatedTradingEndpointTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = $"{nameof(SimulatedTradingEndpointTests)}-{Guid.NewGuid()}";
        _factory = factory.WithWebHostBuilder(builder =>
            builder
                .UseInMemoryMarketStateForTests()
                .UseInMemoryInvestViewDbForTests(databaseName)
                .ConfigureAppConfiguration((_, configurationBuilder) =>
                    configurationBuilder.AddInMemoryCollection(CreateTestConfiguration())));
    }

    [Fact]
    public async Task Portfolio_WhenBearerTokenIsMissing_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/portfolio");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PlaceBuyOrder_WhenCashIsSufficient_FillsAndUpdatesPortfolio()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsDemoUserAsync(client);

        var orderResponse = await client.PostAsJsonAsync(
            "/api/orders",
            new PlaceOrderRequest(" hpg ", " g1 ", "Buy", 100, null));
        using var orderPayload = JsonDocument.Parse(await orderResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        Assert.Equal("HPG", orderPayload.RootElement.GetProperty("symbol").GetString());
        Assert.Equal("G1", orderPayload.RootElement.GetProperty("boardId").GetString());
        Assert.Equal("Buy", orderPayload.RootElement.GetProperty("side").GetString());
        Assert.Equal("Filled", orderPayload.RootElement.GetProperty("status").GetString());
        Assert.Equal(100, orderPayload.RootElement.GetProperty("filledQuantity").GetInt64());
        Assert.Equal(29_150m, orderPayload.RootElement.GetProperty("averageFillPrice").GetDecimal());
        var execution = Assert.Single(orderPayload.RootElement.GetProperty("executions").EnumerateArray());
        Assert.Equal(2_915_000m, execution.GetProperty("grossAmount").GetDecimal());

        using var portfolioPayload = JsonDocument.Parse(await (await client.GetAsync("/api/portfolio")).Content.ReadAsStringAsync());
        Assert.Equal(97_085_000m, portfolioPayload.RootElement.GetProperty("totalCash").GetDecimal());
        Assert.Equal(2_915_000m, portfolioPayload.RootElement.GetProperty("totalMarketValue").GetDecimal());
        Assert.Equal(100_000_000m, portfolioPayload.RootElement.GetProperty("totalEquity").GetDecimal());

        var holding = Assert.Single(portfolioPayload.RootElement.GetProperty("holdings").EnumerateArray());
        Assert.Equal("HPG", holding.GetProperty("symbol").GetString());
        Assert.Equal(100, holding.GetProperty("quantity").GetInt64());
        Assert.Equal(29_150m, holding.GetProperty("averageCost").GetDecimal());
    }

    [Fact]
    public async Task PlaceBuyOrder_WhenCashIsInsufficient_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsDemoUserAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/orders",
            new PlaceOrderRequest("VCB", "G1", "Buy", 10_000, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PlaceSellOrder_WhenHoldingIsInsufficient_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsDemoUserAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/orders",
            new PlaceOrderRequest("HPG", "G1", "Sell", 1, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CancelOrder_WhenLimitOrderIsPending_CancelsWithoutPortfolioMutation()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsDemoUserAsync(client);

        var orderResponse = await client.PostAsJsonAsync(
            "/api/orders",
            new PlaceOrderRequest("HPG", "G1", "Buy", 100, 28_000m));
        using var orderPayload = JsonDocument.Parse(await orderResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        Assert.Equal("New", orderPayload.RootElement.GetProperty("status").GetString());

        var orderId = orderPayload.RootElement.GetProperty("id").GetGuid();
        var cancelResponse = await client.PostAsync($"/api/orders/{orderId}/cancel", content: null);
        using var cancelPayload = JsonDocument.Parse(await cancelResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        Assert.Equal("Cancelled", cancelPayload.RootElement.GetProperty("status").GetString());

        using var portfolioPayload = JsonDocument.Parse(await (await client.GetAsync("/api/portfolio")).Content.ReadAsStringAsync());
        Assert.Equal(100_000_000m, portfolioPayload.RootElement.GetProperty("totalCash").GetDecimal());
        Assert.Empty(portfolioPayload.RootElement.GetProperty("holdings").EnumerateArray());
    }

    [Fact]
    public async Task GetOrders_AfterPlacedOrder_ReturnsOrderHistory()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsDemoUserAsync(client);

        await client.PostAsJsonAsync(
            "/api/orders",
            new PlaceOrderRequest("SSI", "G1", "Buy", 10, null));

        using var ordersPayload = JsonDocument.Parse(await (await client.GetAsync("/api/orders")).Content.ReadAsStringAsync());
        var order = Assert.Single(ordersPayload.RootElement.EnumerateArray());
        Assert.Equal("SSI", order.GetProperty("symbol").GetString());
        Assert.Equal("Filled", order.GetProperty("status").GetString());
    }

    private static async Task AuthorizeAsDemoUserAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/demo-login",
            new DemoLoginRequest(DemoEmail, DemoPassword));
        response.EnsureSuccessStatusCode();

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var accessToken = payload.RootElement.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("Demo login response did not include an access token.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private static Dictionary<string, string?> CreateTestConfiguration()
    {
        return new Dictionary<string, string?>
        {
            ["MarketData:Provider"] = "Mock",
            ["MarketData:QuoteStream:Enabled"] = "false",
            ["DemoAuth:SeedOnStartup"] = "true",
            ["DemoAuth:Email"] = DemoEmail,
            ["DemoAuth:Password"] = DemoPassword,
            ["DemoAuth:DisplayName"] = "InvestView Demo",
            ["DemoAuth:InitialCashBalance"] = "100000000",
            ["DemoAuth:Currency"] = "VND",
            ["Jwt:Issuer"] = "InvestView.Tests",
            ["Jwt:Audience"] = "InvestView.Tests",
            ["Jwt:SigningKey"] = "investview-tests-signing-key-with-64-characters-minimum-value",
            ["Jwt:AccessTokenMinutes"] = "60"
        };
    }

    private sealed record DemoLoginRequest(string Email, string Password);

    private sealed record PlaceOrderRequest(
        string Symbol,
        string BoardId,
        string Side,
        long Quantity,
        decimal? LimitPrice);
}
