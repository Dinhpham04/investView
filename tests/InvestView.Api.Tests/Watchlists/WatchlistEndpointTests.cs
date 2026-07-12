using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace InvestView.Api.Tests.Watchlists;

public sealed class WatchlistEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string DemoEmail = "demo@investview.local";
    private const string DemoPassword = "demo-password";

    private readonly WebApplicationFactory<Program> _factory;

    public WatchlistEndpointTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = $"{nameof(WatchlistEndpointTests)}-{Guid.NewGuid()}";
        _factory = factory.WithWebHostBuilder(builder =>
            builder
                .UseInMemoryMarketStateForTests()
                .UseInMemoryInvestViewDbForTests(databaseName)
                .ConfigureAppConfiguration((_, configurationBuilder) =>
                    configurationBuilder.AddInMemoryCollection(CreateTestConfiguration())));
    }

    [Fact]
    public async Task GetWatchlist_WhenBearerTokenIsMissing_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/watchlist");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WatchlistFlow_WithValidSymbol_CanListAddDuplicateAndRemove()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsDemoUserAsync(client);

        using var initialPayload = JsonDocument.Parse(await (await client.GetAsync("/api/watchlist")).Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, initialPayload.RootElement.ValueKind);
        Assert.Equal(0, initialPayload.RootElement.GetArrayLength());

        var addResponse = await client.PostAsJsonAsync(
            "/api/watchlist",
            new WatchlistItemRequest(" hpg ", " g1 "));
        using var addPayload = JsonDocument.Parse(await addResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
        Assert.Equal("HPG", addPayload.RootElement.GetProperty("symbol").GetString());
        Assert.Equal("G1", addPayload.RootElement.GetProperty("boardId").GetString());

        var itemId = addPayload.RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, itemId);

        var duplicateResponse = await client.PostAsJsonAsync(
            "/api/watchlist",
            new WatchlistItemRequest("HPG", "G1"));
        using var duplicatePayload = JsonDocument.Parse(await duplicateResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        Assert.Equal(itemId, duplicatePayload.RootElement.GetProperty("id").GetGuid());

        using var listPayload = JsonDocument.Parse(await (await client.GetAsync("/api/watchlist")).Content.ReadAsStringAsync());
        var items = listPayload.RootElement.EnumerateArray().ToArray();
        var item = Assert.Single(items);
        Assert.Equal(itemId, item.GetProperty("id").GetGuid());
        Assert.Equal("HPG", item.GetProperty("symbol").GetString());
        Assert.Equal("G1", item.GetProperty("boardId").GetString());

        var deleteResponse = await client.DeleteAsync("/api/watchlist/G1/HPG");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var emptyPayload = JsonDocument.Parse(await (await client.GetAsync("/api/watchlist")).Content.ReadAsStringAsync());
        Assert.Equal(0, emptyPayload.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task AddWatchlistItem_WhenSymbolDoesNotExist_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        await AuthorizeAsDemoUserAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/watchlist",
            new WatchlistItemRequest("ZZZ", "G1"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    private sealed record WatchlistItemRequest(string Symbol, string BoardId);
}
