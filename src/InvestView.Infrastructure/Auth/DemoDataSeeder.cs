using InvestView.Domain.Trading;
using InvestView.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestView.Infrastructure.Auth;

public sealed class DemoDataSeeder
{
    private readonly InvestViewDbContext _dbContext;
    private readonly DemoPasswordHasher _passwordHasher;
    private readonly IOptions<DemoAuthOptions> _options;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(
        InvestViewDbContext dbContext,
        DemoPasswordHasher passwordHasher,
        IOptions<DemoAuthOptions> options,
        ILogger<DemoDataSeeder> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _options = options;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        if (!options.SeedOnStartup)
        {
            return;
        }

        var email = NormalizeEmail(options.Email);
        var user = await _dbContext.Users
            .SingleOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);

        if (user is null)
        {
            user = new UserAccount(
                email,
                options.DisplayName,
                _passwordHasher.HashPassword(options.Password));
            _dbContext.Users.Add(user);
            _dbContext.CashAccounts.Add(new CashAccount(
                user.Id,
                options.Currency,
                options.InitialCashBalance,
                options.InitialCashBalance));

            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded demo user {Email}.", email);
            return;
        }

        var currency = MarketIdentity.NormalizeCurrency(options.Currency);
        var hasCashAccount = await _dbContext.CashAccounts.AnyAsync(
            account => account.UserId == user.Id && account.Currency == currency,
            cancellationToken);
        if (!hasCashAccount)
        {
            _dbContext.CashAccounts.Add(new CashAccount(
                user.Id,
                currency,
                options.InitialCashBalance,
                options.InitialCashBalance));
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded demo cash account for {Email}.", email);
        }
    }

    private static string NormalizeEmail(string email)
    {
        return string.IsNullOrWhiteSpace(email)
            ? throw new InvalidOperationException("DemoAuth:Email is required.")
            : email.Trim().ToLowerInvariant();
    }
}

public static class DemoDataSeederExtensions
{
    public static async Task SeedDemoDataAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
        await seeder.SeedAsync(cancellationToken);
    }
}
