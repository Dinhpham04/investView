namespace InvestView.Domain.Trading;

public sealed class UserAccount
{
    private readonly List<WatchlistItem> _watchlistItems = [];
    private readonly List<CashAccount> _cashAccounts = [];
    private readonly List<Holding> _holdings = [];
    private readonly List<SimulatedOrder> _orders = [];

    private UserAccount()
    {
        Email = string.Empty;
        DisplayName = string.Empty;
        PasswordHash = string.Empty;
    }

    public UserAccount(string email, string displayName, string passwordHash, DateTimeOffset? createdAt = null)
    {
        Id = Guid.NewGuid();
        Email = NormalizeEmail(email);
        DisplayName = RequireText(displayName, nameof(displayName));
        PasswordHash = RequireText(passwordHash, nameof(passwordHash));
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; }

    public string DisplayName { get; private set; }

    public string PasswordHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<WatchlistItem> WatchlistItems => _watchlistItems;

    public IReadOnlyCollection<CashAccount> CashAccounts => _cashAccounts;

    public IReadOnlyCollection<Holding> Holdings => _holdings;

    public IReadOnlyCollection<SimulatedOrder> Orders => _orders;

    private static string NormalizeEmail(string email)
    {
        var normalized = RequireText(email, nameof(email)).Trim().ToLowerInvariant();
        return normalized.Contains('@', StringComparison.Ordinal)
            ? normalized
            : throw new ArgumentException("Email must contain '@'.", nameof(email));
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value.Trim();
    }
}
