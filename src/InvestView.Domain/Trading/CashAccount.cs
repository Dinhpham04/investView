namespace InvestView.Domain.Trading;

public sealed class CashAccount
{
    private CashAccount()
    {
        Currency = string.Empty;
    }

    public CashAccount(
        Guid userId,
        string currency,
        decimal balance,
        decimal availableBalance,
        DateTimeOffset? updatedAt = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (balance < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(balance), "Balance cannot be negative.");
        }

        if (availableBalance < 0m || availableBalance > balance)
        {
            throw new ArgumentOutOfRangeException(nameof(availableBalance), "Available balance must be between zero and balance.");
        }

        Id = Guid.NewGuid();
        UserId = userId;
        Currency = MarketIdentity.NormalizeCurrency(currency);
        Balance = balance;
        AvailableBalance = availableBalance;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Currency { get; private set; }

    public decimal Balance { get; private set; }

    public decimal AvailableBalance { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public UserAccount? User { get; private set; }

    public void Debit(decimal amount, DateTimeOffset? updatedAt = null)
    {
        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        if (amount > AvailableBalance)
        {
            throw new InvalidOperationException("Available cash balance is insufficient.");
        }

        Balance -= amount;
        AvailableBalance -= amount;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
    }

    public void Credit(decimal amount, DateTimeOffset? updatedAt = null)
    {
        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        Balance += amount;
        AvailableBalance += amount;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
    }
}
