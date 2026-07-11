using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace InvestView.Api.Auth;

public static class JwtAuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddInvestViewJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = JwtAuthOptions.FromConfiguration(configuration);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));

        services.AddSingleton(options);
        services.AddSingleton<IJwtTokenIssuer, JwtTokenIssuer>();
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwtOptions =>
            {
                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });
        services.AddAuthorization();

        return services;
    }
}
