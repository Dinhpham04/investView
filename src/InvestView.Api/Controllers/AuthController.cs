using InvestView.Api.Auth;
using InvestView.Application.Abstractions.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestView.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IDemoAuthService _authService;
    private readonly IJwtTokenIssuer _tokenIssuer;

    public AuthController(IDemoAuthService authService, IJwtTokenIssuer tokenIssuer)
    {
        _authService = authService;
        _tokenIssuer = tokenIssuer;
    }

    [AllowAnonymous]
    [HttpPost("demo-login")]
    [ProducesResponseType<DemoLoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DemoLoginResponse>> DemoLogin(
        [FromBody] DemoLoginRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _authService.AuthenticateAsync(request.Email, request.Password, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        var token = _tokenIssuer.IssueToken(user);
        return Ok(new DemoLoginResponse(
            token.AccessToken,
            "Bearer",
            token.ExpiresAt,
            new DemoUserResponse(user.Id, user.Email, user.DisplayName)));
    }
}

public sealed record DemoLoginRequest(string Email, string Password);

public sealed record DemoLoginResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    DemoUserResponse User);

public sealed record DemoUserResponse(Guid Id, string Email, string DisplayName);
