using InvestView.Application.Abstractions.Realtime;

namespace InvestView.Infrastructure.Realtime;

public sealed class MarketQuoteSubscriptionRegistry : IMarketQuoteSubscriptionRegistry
{
    private const string DefaultBoardId = "G1";
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, ConnectionSubscription> _connections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ConnectionOhlcSubscription> _ohlcConnections = new(StringComparer.Ordinal);
    private TaskCompletionSource<MarketQuoteSubscriptionSnapshot> _changeSignal =
        NewChangeSignal();
    private long _version;

    public MarketQuoteConnectionSubscriptionChange SetConnectionSubscription(
        string connectionId,
        string? boardId,
        IReadOnlyCollection<string>? symbols)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        lock (_syncRoot)
        {
            var normalizedBoardId = NormalizeBoardId(boardId);
            var normalizedSymbols = NormalizeSymbols(symbols);
            _connections.TryGetValue(connectionId, out var previous);

            if (previous is not null
                && previous.BoardId == normalizedBoardId
                && previous.Symbols.SequenceEqual(normalizedSymbols, StringComparer.Ordinal))
            {
                return new MarketQuoteConnectionSubscriptionChange(
                    previous.BoardId,
                    previous.Symbols,
                    normalizedBoardId,
                    normalizedSymbols,
                    CreateSnapshot());
            }

            if (normalizedSymbols.Count == 0)
            {
                _connections.Remove(connectionId);
            }
            else
            {
                _connections[connectionId] = new ConnectionSubscription(normalizedBoardId, normalizedSymbols);
            }

            var snapshot = PublishChange();
            return new MarketQuoteConnectionSubscriptionChange(
                previous?.BoardId,
                previous?.Symbols ?? [],
                normalizedBoardId,
                normalizedSymbols,
                snapshot);
        }
    }

    public MarketQuoteSubscriptionSnapshot SetConnectionOhlcSubscription(
        string connectionId,
        string? symbol,
        IReadOnlyCollection<string>? resolutions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        lock (_syncRoot)
        {
            var normalizedSymbol = NormalizeSymbol(symbol);
            var normalizedResolutions = NormalizeResolutions(resolutions);
            _ohlcConnections.TryGetValue(connectionId, out var previous);

            if (previous is not null
                && previous.Symbol == normalizedSymbol
                && previous.Resolutions.SequenceEqual(normalizedResolutions, StringComparer.Ordinal))
            {
                return CreateSnapshot();
            }

            if (string.IsNullOrWhiteSpace(normalizedSymbol) || normalizedResolutions.Count == 0)
            {
                if (!_ohlcConnections.Remove(connectionId))
                {
                    return CreateSnapshot();
                }
            }
            else
            {
                _ohlcConnections[connectionId] = new ConnectionOhlcSubscription(normalizedSymbol, normalizedResolutions);
            }

            return PublishChange();
        }
    }

    public MarketQuoteSubscriptionSnapshot RemoveConnection(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        lock (_syncRoot)
        {
            var removedBoardSubscription = _connections.Remove(connectionId);
            var removedOhlcSubscription = _ohlcConnections.Remove(connectionId);
            if (!removedBoardSubscription && !removedOhlcSubscription)
            {
                return CreateSnapshot();
            }

            return PublishChange();
        }
    }

    public MarketQuoteSubscriptionSnapshot GetSnapshot()
    {
        lock (_syncRoot)
        {
            return CreateSnapshot();
        }
    }

    public ValueTask<MarketQuoteSubscriptionSnapshot> WaitForChangeAsync(
        long lastObservedVersion,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            var snapshot = CreateSnapshot();
            if (snapshot.Version != lastObservedVersion)
            {
                return ValueTask.FromResult(snapshot);
            }

            return new ValueTask<MarketQuoteSubscriptionSnapshot>(
                _changeSignal.Task.WaitAsync(cancellationToken));
        }
    }

    private MarketQuoteSubscriptionSnapshot PublishChange()
    {
        _version++;
        var snapshot = CreateSnapshot();
        var previousSignal = _changeSignal;
        _changeSignal = NewChangeSignal();
        previousSignal.TrySetResult(snapshot);

        return snapshot;
    }

    private MarketQuoteSubscriptionSnapshot CreateSnapshot()
    {
        var boards = _connections
            .Values
            .GroupBy(subscription => subscription.BoardId, StringComparer.Ordinal)
            .Select(group => new MarketQuoteBoardSubscription(
                group.Key,
                group
                    .SelectMany(subscription => subscription.Symbols)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray()))
            .Where(board => board.Symbols.Count > 0)
            .OrderBy(board => board.BoardId, StringComparer.Ordinal)
            .ToArray();

        var ohlcSubscriptions = _ohlcConnections
            .Values
            .GroupBy(subscription => subscription.Symbol, StringComparer.Ordinal)
            .Select(group => new MarketOhlcSubscription(
                group.Key,
                group
                    .SelectMany(subscription => subscription.Resolutions)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray()))
            .Where(subscription => subscription.Resolutions.Count > 0)
            .OrderBy(subscription => subscription.Symbol, StringComparer.Ordinal)
            .ToArray();

        return new MarketQuoteSubscriptionSnapshot(boards, ohlcSubscriptions, _version);
    }

    private static string NormalizeBoardId(string? boardId)
    {
        return string.IsNullOrWhiteSpace(boardId)
            ? DefaultBoardId
            : boardId.Trim().ToUpperInvariant();
    }

    private static IReadOnlyList<string> NormalizeSymbols(IReadOnlyCollection<string>? symbols)
    {
        return symbols is null
            ? []
            : symbols
                .SelectMany(symbol => symbol.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Select(symbol => symbol.Trim().ToUpperInvariant())
                .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
    }

    private static string NormalizeSymbol(string? symbol)
    {
        return string.IsNullOrWhiteSpace(symbol)
            ? string.Empty
            : symbol.Trim().ToUpperInvariant();
    }

    private static IReadOnlyList<string> NormalizeResolutions(IReadOnlyCollection<string>? resolutions)
    {
        return resolutions is null
            ? []
            : resolutions
                .Select(resolution => resolution.Trim().ToUpperInvariant())
                .Where(resolution => !string.IsNullOrWhiteSpace(resolution))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
    }

    private static TaskCompletionSource<MarketQuoteSubscriptionSnapshot> NewChangeSignal()
    {
        return new TaskCompletionSource<MarketQuoteSubscriptionSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed record ConnectionSubscription(
        string BoardId,
        IReadOnlyList<string> Symbols);

    private sealed record ConnectionOhlcSubscription(
        string Symbol,
        IReadOnlyList<string> Resolutions);
}
