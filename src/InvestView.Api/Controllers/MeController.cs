using System.Security.Claims;
using InvestView.Application.Abstractions.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestView.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
[Produces("application/json")]
public sealed class MeController : ControllerBase
{
    private readonly IDemoAuthService _authService;

    public MeController(IDemoAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MeResponse>> Get(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var profile = await _authService.GetProfileAsync(userId, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        return Ok(new MeResponse(
            profile.Id,
            profile.Email,
            profile.DisplayName,
            profile.CashAccounts
                .Select(account => new MeCashAccountResponse(
                    account.Currency,
                    account.Balance,
                    account.AvailableBalance))
                .ToArray()));
    }
}

public sealed record MeResponse(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<MeCashAccountResponse> CashAccounts);

public sealed record MeCashAccountResponse(string Currency, decimal Balance, decimal AvailableBalance);
