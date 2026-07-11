using InvestView.Application.Abstractions.Auth;
using InvestView.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestView.Infrastructure.Auth;

public sealed class DemoAuthService : IDemoAuthService
{
    private readonly InvestViewDbContext _dbContext;
    private readonly DemoPasswordHasher _passwordHasher;

    public DemoAuthService(InvestViewDbContext dbContext, DemoPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    public async Task<DemoAuthenticatedUser?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var normalizedEmail = NormalizeEmail(email);
        var user = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Email == normalizedEmail, cancellationToken);
        if (user is null || !_passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            return null;
        }

        return new DemoAuthenticatedUser(user.Id, user.Email, user.DisplayName);
    }

    public async Task<DemoUserProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(account => account.CashAccounts)
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var cashAccounts = user.CashAccounts
            .OrderBy(account => account.Currency, StringComparer.Ordinal)
            .Select(account => new DemoCashAccount(
                account.Currency,
                account.Balance,
                account.AvailableBalance))
            .ToArray();

        return new DemoUserProfile(user.Id, user.Email, user.DisplayName, cashAccounts);
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
