using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InvestView.Application.Abstractions.Auth;
using Microsoft.IdentityModel.Tokens;

namespace InvestView.Api.Auth;

public interface IJwtTokenIssuer
{
    JwtTokenResult IssueToken(DemoAuthenticatedUser user);
}

public sealed class JwtTokenIssuer : IJwtTokenIssuer
{
    private readonly JwtAuthOptions _options;

    public JwtTokenIssuer(JwtAuthOptions options)
    {
        _options = options;
    }

    public JwtTokenResult IssueToken(DemoAuthenticatedUser user)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
            new Claim(ClaimTypes.Name, user.DisplayName)
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new JwtTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

public sealed record JwtTokenResult(string AccessToken, DateTimeOffset ExpiresAt);
