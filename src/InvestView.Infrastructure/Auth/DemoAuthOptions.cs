using System.Globalization;

namespace InvestView.Infrastructure.Auth;

public sealed class DemoAuthOptions
{
    public const string SectionName = "DemoAuth";

    public bool SeedOnStartup { get; set; } = true;

    public string Email { get; set; } = "demo@investview.local";

    public string DisplayName { get; set; } = "InvestView Demo";

    public string Password { get; set; } = "demo-password";

    public string Currency { get; set; } = "VND";

    public decimal InitialCashBalance { get; set; } = 100_000_000m;

    public void ApplyEnvironment()
    {
        Email = FirstConfiguredValue(Email, Environment.GetEnvironmentVariable("INVESTVIEW_DEMO_EMAIL"));
        DisplayName = FirstConfiguredValue(DisplayName, Environment.GetEnvironmentVariable("INVESTVIEW_DEMO_DISPLAY_NAME"));
        Password = FirstConfiguredValue(Password, Environment.GetEnvironmentVariable("INVESTVIEW_DEMO_PASSWORD"));
        Currency = FirstConfiguredValue(Currency, Environment.GetEnvironmentVariable("INVESTVIEW_DEMO_CURRENCY"));

        var initialCashBalance = Environment.GetEnvironmentVariable("INVESTVIEW_DEMO_INITIAL_CASH_BALANCE");
        if (decimal.TryParse(initialCashBalance, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedBalance))
        {
            InitialCashBalance = parsedBalance;
        }
    }

    private static string FirstConfiguredValue(string configuredValue, string? environmentValue)
    {
        return string.IsNullOrWhiteSpace(environmentValue)
            ? configuredValue
            : environmentValue;
    }
}
