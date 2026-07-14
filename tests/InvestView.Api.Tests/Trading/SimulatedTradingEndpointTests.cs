using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Dtos.MarketData;
using InvestView.Domain.Trading;
using InvestView.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
                .ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(
                        _ => new FixedTimeProvider(new DateTimeOffset(2026, 7, 14, 3, 0, 0, TimeSpan.Zero)));
                })
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
        await SeedMarketSessionAsync(isOpen: true);

        var orderResponse = await client.PostAsJsonAsync(
            "/api/orders",
            new PlaceOrderRequest(" hpg ", " g1 ", "Buy", "MTL", 100, null));
        using var orderPayload = JsonDocument.Parse(await orderResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        Assert.Equal("HPG", orderPayload.RootElement.GetProperty("symbol").GetString());
        Assert.Equal("G1", orderPayload.RootElement.GetProperty("boardId").GetString());
        Assert.Equal("Buy", orderPayload.RootElement.GetProperty("side").GetString());
        Assert.Equal("MTL", orderPayload.RootElement.GetProperty("orderType").GetString());
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
        Assert.Equal(0, holding.GetProperty("availableQuantity").GetInt64());
        Assert.Equal(100, holding.GetProperty("pendingReceiveQuantity").GetInt64());
        Assert.Equal(29_150m, holding.GetProperty("averageCost").GetDecimal());
    }

    [Fact]
    public async Task PlaceBuyOrder_WhenCashIsInsufficient_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsDemoUserAsync(client);
        await SeedMarketSessionAsync(isOpen: true);

        var response = await client.PostAsJsonAsync(
            "/api/orders",
            new PlaceOrderRequest("VCB", "G1", "Buy", "MTL", 10_000, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PlaceSellOrder_WhenHoldingIsInsufficient_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsDemoUserAsync(client);
        await SeedMarketSessionAsync(isOpen: true);

        var response = await client.PostAsJsonAsync(
            "/api/orders",
            new PlaceOrderRequest("HPG", "G1", "Sell", "MTL", 1, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PlaceSellOrder_WhenBoughtSharesArePendingReceive_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsDemoUserAsync(client);
        await SeedMarketSessionAsync(isOpen: true);

        var buyResponse = await client.PostAsJsonAsync(
            "/api/orders",
            new PlaceOrderRequest("HPG", "G1", "Buy", "MTL", 100, null));
        Assert.Equal(HttpStatusCode.Created, buyResponse.StatusCode);

        var sellResponse = await client.PostAsJsonAsync(
            "/api/orders",
            new PlaceOrderRequest("HPG", "G1", "Sell", "MTL", 1, null));

        Assert.Equal(HttpStatusCode.BadRequest, sellResponse.StatusCode);
    }

    [Fact]
    public async Task CancelOrder_WhenLimitOrderIsPending_CancelsWithoutPortfolioMutation()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsDemoUserAsync(client);
        await SeedMarketSessionAsync(isOpen: true);

        var orderResponse = await client.PostAsJsonAsync(
            "/api/orders",
            new PlaceOrderRequest("HPG", "G1", "Buy", "LO", 100, 28_000m));
        using var orderPayload = JsonDocument.Parse(await orderResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        Assert.Equal("LO", orderPayload.RootElement.GetProperty("orderType").GetString());
        Assert.Equal("New", orderPayload.RootElement.GetProperty("status").GetString());

        using var reservedPortfolioPayload = JsonDocument.Parse(await (await client.GetAsync("/api/portfolio")).Content.ReadAsStringAsync());
        Assert.Equal(100_000_000m, reservedPortfolioPayload.RootElement.GetProperty("totalCash").GetDecimal());
        Assert.Equal(97_200_000m, reservedPortfolioPayload.RootElement.GetProperty("totalAvailableCash").GetDecimal());
        Assert.Empty(reservedPortfolioPayload.RootElement.GetProperty("holdings").EnumerateArray());

        var orderId = orderPayload.RootElement.GetProperty("id").GetGuid();
        var cancelResponse = await client.PostAsync($"/api/orders/{orderId}/cancel", content: null);
        using var cancelPayload = JsonDocument.Parse(await cancelResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        Assert.Equal("Cancelled", cancelPayload.RootElement.GetProperty("status").GetString());

        using var portfolioPayload = JsonDocument.Parse(await (await client.GetAsync("/api/portfolio")).Content.ReadAsStringAsync());
        Assert.Equal(100_000_000m, portfolioPayload.RootElement.GetProperty("totalCash").GetDecimal());
        Assert.Equal(100_000_000m, portfolioPayload.RootElement.GetProperty("totalAvailableCash").GetDecimal());
        Assert.Empty(portfolioPayload.RootElement.GetProperty("holdings").EnumerateArray());
    }

    [Fact]
    public async Task PlaceBuyOrder_WhenPendingBuyReservesCash_PreventsOverspendingUntilCancelled()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsDemoUserAsync(client);
        await SeedMarketSessionAsync(isOpen: true);

        var firstOrderResponse = await client.PostAsJsonAsync(
            "/api/orders",
            new PlaceOrderRequest("HPG", "G1", "Buy", "LO", 3_000, 28_000m));
        using var firstOrderPayload = JsonDocument.Parse(await firstOrderResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Created, firstOrderResponse.StatusCode);
        Assert.Equal("New", firstOrderPayload.RootElement.GetProperty("status").GetString());

        var secondOrderResponse = await client.PostAsJsonAsync(
            "/api/orders",
            new PlaceOrderRequest("HPG", "G1", "Buy", "LO", 1_000, 28_000m));

        Assert.Equal(HttpStatusCode.BadRequest, secondOrderResponse.StatusCode);

        var orderId = firstOrderPayload.RootElement.GetProperty("id").GetGuid();
        var cancelResponse = await client.PostAsync($"/api/orders/{orderId}/cancel", content: null);

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task PlaceSellOrder_WhenPendingSellReservesHolding_PreventsOversellingUntilCancelled()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsDemoUserAsync(client);
        await SeedMarketSessionAsync(isOpen: true);
        await SeedAvailableHoldingAsync("HPG", "G1", 100, 29_150m);

        var sellResponse = await client.PostAsJsonAsync(
            "/api/orders",
            new PlaceOrderRequest("HPG", "G1", "Sell", "LO", 100, 30_000m));
        using var sellPayload = JsonDocument.Parse(await sellResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Created, sellResponse.StatusCode);
        Assert.Equal("New", sellPayload.RootElement.GetProperty("status").GetString());

        using var reservedPortfolioPayload = JsonDocument.Parse(await (await client.GetAsync("/api/portfolio")).Content.ReadAsStringAsync());
        var reservedHolding = Assert.Single(reservedPortfolioPayload.RootElement.GetProperty("holdings").EnumerateArray());
        Assert.Equal(100, reservedHolding.GetProperty("quantity").GetInt64());
        Assert.Equal(0, reservedHolding.GetProperty("availableQuantity").GetInt64());

        var secondSellResponse = await client.PostAsJsonAsync(
            "/api/orders",
            new PlaceOrderRequest("HPG", "G1", "Sell", "LO", 1, 30_000m));

        Assert.Equal(HttpStatusCode.BadRequest, secondSellResponse.StatusCode);

        var orderId = sellPayload.RootElement.GetProperty("id").GetGuid();
        var cancelResponse = await client.PostAsync($"/api/orders/{orderId}/cancel", content: null);

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        using var releasedPortfolioPayload = JsonDocument.Parse(await (await client.GetAsync("/api/portfolio")).Content.ReadAsStringAsync());
        var releasedHolding = Assert.Single(releasedPortfolioPayload.RootElement.GetProperty("holdings").EnumerateArray());
        Assert.Equal(100, releasedHolding.GetProperty("quantity").GetInt64());
        Assert.Equal(100, releasedHolding.GetProperty("availableQuantity").GetInt64());
    }

    [Fact]
    public async Task PlaceSellOrder_WhenHoldingIsAvailable_FillsAndCreditsCashImmediately()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsDemoUserAsync(client);
        await SeedMarketSessionAsync(isOpen: true);
        await SeedAvailableHoldingAsync("HPG", "G1", 100, 20_000m);

        var sellResponse = await client.PostAsJsonAsync(
            "/api/orders",
            new PlaceOrderRequest("HPG", "G1", "Sell", "MTL", 40, null));
        using var sellPayload = JsonDocument.Parse(await sellResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Created, sellResponse.StatusCode);
        Assert.Equal("Filled", sellPayload.RootElement.GetProperty("status").GetString());
        Assert.Equal(40, sellPayload.RootElement.GetProperty("filledQuantity").GetInt64());

        using var portfolioPayload = JsonDocument.Parse(await (await client.GetAsync("/api/portfolio")).Content.ReadAsStringAsync());
        Assert.Equal(101_166_000m, portfolioPayload.RootElement.GetProperty("totalCash").GetDecimal());
        Assert.Equal(101_166_000m, portfolioPayload.RootElement.GetProperty("totalAvailableCash").GetDecimal());

        var holding = Assert.Single(portfolioPayload.RootElement.GetProperty("holdings").EnumerateArray());
        Assert.Equal(60, holding.GetProperty("quantity").GetInt64());
        Assert.Equal(60, holding.GetProperty("availableQuantity").GetInt64());
        Assert.Equal(0, holding.GetProperty("pendingReceiveQuantity").GetInt64());
    }

    [Fact]
    public async Task PlaceOrder_WhenMarketSessionIsClosed_ReturnsBadRequestWithoutCreatingOrder()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsDemoUserAsync(client);
        await SeedMarketSessionAsync(isOpen: false);

        var orderResponse = await client.PostAsJsonAsync(
            "/api/orders",
            new PlaceOrderRequest("HPG", "G1", "Buy", "MTL", 100, null));
        using var orderPayload = JsonDocument.Parse(await orderResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, orderResponse.StatusCode);
        Assert.Equal("Market is not open for simulated orders.", orderPayload.RootElement.GetProperty("title").GetString());

        using var ordersPayload = JsonDocument.Parse(await (await client.GetAsync("/api/orders")).Content.ReadAsStringAsync());
        Assert.Empty(ordersPayload.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task GetOrders_AfterPlacedOrder_ReturnsOrderHistory()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsDemoUserAsync(client);
        await SeedMarketSessionAsync(isOpen: true);

        await client.PostAsJsonAsync(
            "/api/orders",
            new PlaceOrderRequest("SSI", "G1", "Buy", "MTL", 10, null));

        using var ordersPayload = JsonDocument.Parse(await (await client.GetAsync("/api/orders")).Content.ReadAsStringAsync());
        var order = Assert.Single(ordersPayload.RootElement.EnumerateArray());
        Assert.Equal("SSI", order.GetProperty("symbol").GetString());
        Assert.Equal("MTL", order.GetProperty("orderType").GetString());
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

    private async Task SeedAvailableHoldingAsync(
        string symbol,
        string boardId,
        long quantity,
        decimal averageCost)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InvestViewDbContext>();
        var user = await dbContext.Users.SingleAsync(user => user.Email == DemoEmail);
        dbContext.Holdings.Add(new Holding(user.Id, symbol, boardId, quantity, quantity, averageCost));
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedMarketSessionAsync(bool isOpen)
    {
        using var scope = _factory.Services.CreateScope();
        var marketStateStore = scope.ServiceProvider.GetRequiredService<IMarketStateStore>();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
        await marketStateStore.ApplyMarketSessionUpdateAsync(
            new MarketSessionUpdateDto(
                MarketId: "VN",
                BoardId: "G1",
                ProductGroupId: "STO",
                EventId: isOpen ? "AB2" : "CLOSED",
                TradingSessionId: isOpen ? "40" : "99",
                UpdatedAt: now,
                Phase: isOpen ? MarketSessionPhases.Continuous : MarketSessionPhases.Closed,
                Label: isOpen ? "Continuous" : "Closed",
                IsOpen: isOpen,
                IsContinuous: isOpen,
                Source: "TEST"),
            CancellationToken.None);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
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
        string OrderType,
        long Quantity,
        decimal? LimitPrice);
}
