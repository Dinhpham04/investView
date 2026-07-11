namespace InvestView.Application.Abstractions.Auth;

public interface IDemoAuthService
{
    Task<DemoAuthenticatedUser?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken);

    Task<DemoUserProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record DemoAuthenticatedUser(Guid Id, string Email, string DisplayName);

public sealed record DemoUserProfile(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<DemoCashAccount> CashAccounts);

public sealed record DemoCashAccount(string Currency, decimal Balance, decimal AvailableBalance);
