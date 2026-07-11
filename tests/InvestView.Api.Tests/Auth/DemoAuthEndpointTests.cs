using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace InvestView.Api.Tests.Auth;

public sealed class DemoAuthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string DemoEmail = "demo@investview.local";
    private const string DemoPassword = "demo-password";
    private const string DemoDisplayName = "InvestView Demo";
    private const decimal InitialCashBalance = 100_000_000m;

    private readonly WebApplicationFactory<Program> _factory;

    public DemoAuthEndpointTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = $"{nameof(DemoAuthEndpointTests)}-{Guid.NewGuid()}";
        _factory = factory.WithWebHostBuilder(builder =>
            builder
                .UseInMemoryMarketStateForTests()
                .UseInMemoryInvestViewDbForTests(databaseName)
                .ConfigureAppConfiguration((_, configurationBuilder) =>
                    configurationBuilder.AddInMemoryCollection(CreateTestConfiguration())));
    }

    [Fact]
    public async Task DemoLogin_WhenCredentialsAreValid_ReturnsBearerToken()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/demo-login",
            new DemoLoginRequest(DemoEmail, DemoPassword));
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer", payload.RootElement.GetProperty("tokenType").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.RootElement.GetProperty("accessToken").GetString()));
        Assert.Equal(DemoEmail, payload.RootElement.GetProperty("user").GetProperty("email").GetString());
        Assert.Equal(DemoDisplayName, payload.RootElement.GetProperty("user").GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task DemoLogin_WhenPasswordIsInvalid_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/demo-login",
            new DemoLoginRequest(DemoEmail, "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WhenBearerTokenIsMissing_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WhenBearerTokenIsValid_ReturnsSeededUserAndCash()
    {
        using var client = _factory.CreateClient();
        var token = await LoginAndReadAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/me");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(DemoEmail, payload.RootElement.GetProperty("email").GetString());

        var cashAccounts = payload.RootElement.GetProperty("cashAccounts");
        var cashAccount = Assert.Single(cashAccounts.EnumerateArray());
        Assert.Equal("VND", cashAccount.GetProperty("currency").GetString());
        Assert.Equal(InitialCashBalance, cashAccount.GetProperty("balance").GetDecimal());
        Assert.Equal(InitialCashBalance, cashAccount.GetProperty("availableBalance").GetDecimal());
    }

    [Fact]
    public async Task GetMarketQuotes_WhenJwtIsEnabled_RemainsAnonymous()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/market/quotes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task QuoteHubNegotiate_WhenJwtIsEnabled_RemainsAnonymous()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync("/hubs/quotes/negotiate?negotiateVersion=1", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<string> LoginAndReadAccessTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/demo-login",
            new DemoLoginRequest(DemoEmail, DemoPassword));
        response.EnsureSuccessStatusCode();

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return payload.RootElement.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("Demo login response did not include an access token.");
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
            ["DemoAuth:DisplayName"] = DemoDisplayName,
            ["DemoAuth:InitialCashBalance"] = InitialCashBalance.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["DemoAuth:Currency"] = "VND",
            ["Jwt:Issuer"] = "InvestView.Tests",
            ["Jwt:Audience"] = "InvestView.Tests",
            ["Jwt:SigningKey"] = "investview-tests-signing-key-with-64-characters-minimum-value",
            ["Jwt:AccessTokenMinutes"] = "60"
        };
    }

    private sealed record DemoLoginRequest(string Email, string Password);
}
