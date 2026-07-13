namespace InvestView.Domain.Trading;

public sealed class WatchlistGroup
{
    private readonly List<WatchlistItem> _items = [];

    private WatchlistGroup()
    {
        Name = string.Empty;
    }

    public WatchlistGroup(Guid userId, string name, DateTimeOffset? createdAt = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        var timestamp = createdAt ?? DateTimeOffset.UtcNow;
        Id = Guid.NewGuid();
        UserId = userId;
        Name = NormalizeName(name);
        CreatedAt = timestamp;
        UpdatedAt = timestamp;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Name { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<WatchlistItem> Items => _items;

    public UserAccount? User { get; private set; }

    public static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Watchlist name is required.", nameof(name));
        }

        return name.Trim();
    }
}
