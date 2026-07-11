using System.Security.Cryptography;

namespace InvestView.Api.Auth;

public sealed class JwtAuthOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "InvestView";

    public string Audience { get; set; } = "InvestView.Web";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 480;

    public static JwtAuthOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new JwtAuthOptions();
        configuration.GetSection(SectionName).Bind(options);
        options.SigningKey = FirstConfiguredValue(
            options.SigningKey,
            Environment.GetEnvironmentVariable("INVESTVIEW_JWT_SIGNING_KEY"));

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            options.SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        if (options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 characters.");
        }

        if (options.AccessTokenMinutes <= 0)
        {
            options.AccessTokenMinutes = 480;
        }

        return options;
    }

    private static string FirstConfiguredValue(string configuredValue, string? environmentValue)
    {
        return string.IsNullOrWhiteSpace(environmentValue)
            ? configuredValue
            : environmentValue;
    }
}
