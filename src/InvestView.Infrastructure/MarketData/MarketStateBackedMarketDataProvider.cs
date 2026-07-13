using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Dtos.MarketData;
using Microsoft.Extensions.Logging;

namespace InvestView.Infrastructure.MarketData;

public sealed class MarketStateBackedMarketDataProvider : IMarketDataProvider
{
    private readonly IMarketDataProvider _fallbackProvider;
    private readonly IMarketStateMirror _localMirror;
    private readonly IMarketStateStore _sharedStateStore;
    private readonly ILogger<MarketStateBackedMarketDataProvider> _logger;
    private readonly ISymbolMetadataProvider? _metadataProvider;

    public MarketStateBackedMarketDataProvider(
        IMarketDataProvider fallbackProvider,
        IMarketStateMirror localMirror,
        IMarketStateStore sharedStateStore,
        ILogger<MarketStateBackedMarketDataProvider> logger,
        ISymbolMetadataProvider? metadataProvider = null)
    {
        _fallbackProvider = fallbackProvider;
        _localMirror = localMirror;
        _sharedStateStore = sharedStateStore;
        _logger = logger;
        _metadataProvider = metadataProvider;
    }

    public async Task<IReadOnlyList<MarketQuoteDto>> GetMarketBoardAsync(
        MarketBoardQuery query,
        CancellationToken cancellationToken)
    {
        var normalizedBoardId = NormalizeToken(query.BoardId, MockMarketDataProvider.DefaultBoardId);
        var requestedSymbols = await ResolveRequestedSymbolsAsync(query, cancellationToken);

        if (requestedSymbols.Count > 0)
        {
            var quotesBySymbol = new Dictionary<string, MarketQuoteDto>(StringComparer.Ordinal);
            var localQuotes = await _localMirror.GetQuotesAsync(normalizedBoardId, requestedSymbols, cancellationToken);
            AddUsableQuotes(quotesBySymbol, localQuotes);

            var missingSymbols = MissingSymbols(requestedSymbols, quotesBySymbol);
            if (missingSymbols.Count > 0)
            {
                var sharedQuotes = await _sharedStateStore.GetQuotesAsync(normalizedBoardId, missingSymbols, cancellationToken);
                var usableSharedQuotes = sharedQuotes.Where(IsUsableQuote).ToArray();
                if (usableSharedQuotes.Length > 0)
                {
                    await _localMirror.UpsertQuotesAsync(usableSharedQuotes, cancellationToken);
                    AddUsableQuotes(quotesBySymbol, usableSharedQuotes);
                }
            }

            missingSymbols = MissingSymbols(requestedSymbols, quotesBySymbol);
            if (missingSymbols.Count == 0)
            {
                _logger.LogDebug("Market board snapshot served from market-state for {BoardId}.", normalizedBoardId);
                return OrderQuotes(quotesBySymbol.Values);
            }

            _logger.LogInformation(
                "Market board snapshot backfilling {MissingCount} missing or unusable symbols for {BoardId}: {Symbols}. REST fallback can call DNSE per-symbol endpoints including foreign-trading.",
                missingSymbols.Count,
                normalizedBoardId,
                FormatSymbols(missingSymbols));
            var fallbackQuery = query with { Symbols = missingSymbols };
            var missingFallbackQuotes = await _fallbackProvider.GetMarketBoardAsync(fallbackQuery, cancellationToken);
            await StoreFallbackQuotesAsync(query, requestedSymbols, missingFallbackQuotes, cancellationToken);
            AddUsableQuotes(quotesBySymbol, missingFallbackQuotes);

            return OrderQuotes(quotesBySymbol.Values);
        }

        _logger.LogInformation(
            "Market board snapshot has no Redis membership for board {BoardId}, market {MarketId}, category {IndexName}; falling back to REST provider.",
            normalizedBoardId,
            string.IsNullOrWhiteSpace(query.MarketId) ? "-" : NormalizeToken(query.MarketId, string.Empty),
            string.IsNullOrWhiteSpace(query.IndexName) ? "-" : NormalizeToken(query.IndexName, string.Empty));
        var fallbackQuotes = await _fallbackProvider.GetMarketBoardAsync(query, cancellationToken);
        await StoreFallbackQuotesAsync(query, [], fallbackQuotes, cancellationToken);
        return OrderQuotes(fallbackQuotes);
    }

    public async Task<SymbolDetailDto?> GetSymbolDetailAsync(
        string symbol,
        string boardId,
        CancellationToken cancellationToken)
    {
        var normalizedSymbol = NormalizeToken(symbol, string.Empty);
        var normalizedBoardId = NormalizeToken(boardId, MockMarketDataProvider.DefaultBoardId);

        var localDetail = await _localMirror.GetSymbolDetailAsync(normalizedSymbol, normalizedBoardId, cancellationToken);
        var sharedDetail = await _sharedStateStore.GetSymbolDetailAsync(normalizedSymbol, normalizedBoardId, cancellationToken);
        if (localDetail is null && sharedDetail is not null)
        {
            await _localMirror.UpsertSymbolDetailAsync(sharedDetail, cancellationToken);
            if (IsUsableDetail(sharedDetail))
            {
                await _localMirror.UpsertQuotesAsync([CreateQuoteFromDetail(sharedDetail)], cancellationToken);
            }
        }

        var metadataDetail = localDetail ?? sharedDetail;
        var localQuotes = await _localMirror.GetQuotesAsync(normalizedBoardId, [normalizedSymbol], cancellationToken);
        var localQuote = localQuotes.FirstOrDefault(IsUsableQuote);
        if (localQuote is not null)
        {
            metadataDetail ??= await BackfillSymbolMetadataAsync(localQuote, cancellationToken);
            return CreateDetailFromQuote(localQuote, metadataDetail);
        }

        var sharedQuotes = await _sharedStateStore.GetQuotesAsync(normalizedBoardId, [normalizedSymbol], cancellationToken);
        var sharedQuote = sharedQuotes.FirstOrDefault(IsUsableQuote);
        if (sharedQuote is not null)
        {
            await _localMirror.UpsertQuotesAsync([sharedQuote], cancellationToken);
            metadataDetail ??= await BackfillSymbolMetadataAsync(sharedQuote, cancellationToken);
            return CreateDetailFromQuote(sharedQuote, metadataDetail);
        }

        if (metadataDetail is not null && IsUsableDetail(metadataDetail))
        {
            return metadataDetail;
        }

        var fallbackDetail = await _fallbackProvider.GetSymbolDetailAsync(normalizedSymbol, normalizedBoardId, cancellationToken);
        if (fallbackDetail is not null)
        {
            await _sharedStateStore.UpsertSymbolDetailAsync(fallbackDetail, cancellationToken);
            await _sharedStateStore.UpsertQuotesAsync([CreateQuoteFromDetail(fallbackDetail)], cancellationToken);
            await _localMirror.UpsertSymbolDetailAsync(fallbackDetail, cancellationToken);
            await _localMirror.UpsertQuotesAsync([CreateQuoteFromDetail(fallbackDetail)], cancellationToken);
        }

        return fallbackDetail;
    }

    public async Task<IReadOnlyList<OhlcBarDto>> GetOhlcAsync(
        string symbol,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var normalizedSymbol = NormalizeToken(symbol, string.Empty);
        var normalizedResolution = NormalizeToken(resolution, "1");

        if (await _localMirror.HasOhlcCoverageAsync(normalizedSymbol, normalizedResolution, from, to, cancellationToken))
        {
            return await _localMirror.GetOhlcBarsAsync(
                normalizedSymbol,
                normalizedResolution,
                from,
                to,
                cancellationToken);
        }

        if (await _sharedStateStore.HasOhlcCoverageAsync(normalizedSymbol, normalizedResolution, from, to, cancellationToken))
        {
            var sharedBars = await _sharedStateStore.GetOhlcBarsAsync(
                normalizedSymbol,
                normalizedResolution,
                from,
                to,
                cancellationToken);
            await _localMirror.UpsertOhlcBarsAsync(
                normalizedSymbol,
                normalizedResolution,
                from,
                to,
                sharedBars,
                cancellationToken);
            return sharedBars;
        }

        var fallbackBars = await _fallbackProvider.GetOhlcAsync(
            normalizedSymbol,
            normalizedResolution,
            from,
            to,
            cancellationToken);
        await _sharedStateStore.UpsertOhlcBarsAsync(
            normalizedSymbol,
            normalizedResolution,
            from,
            to,
            fallbackBars,
            cancellationToken);
        await _localMirror.UpsertOhlcBarsAsync(
            normalizedSymbol,
            normalizedResolution,
            from,
            to,
            fallbackBars,
            cancellationToken);

        return fallbackBars;
    }

    public async Task<IReadOnlyList<MarketIndexDto>> GetMarketIndicesAsync(
        IReadOnlyCollection<string> indexNames,
        CancellationToken cancellationToken)
    {
        var normalizedNames = NormalizeSymbols(indexNames);
        var localIndices = await _localMirror.GetMarketIndicesAsync(normalizedNames, cancellationToken);
        if (normalizedNames.Count == 0 ? localIndices.Count > 0 : HasAllIndices(localIndices, normalizedNames))
        {
            return OrderIndices(localIndices);
        }

        var sharedIndices = await _sharedStateStore.GetMarketIndicesAsync(normalizedNames, cancellationToken);
        if (normalizedNames.Count == 0 ? sharedIndices.Count > 0 : HasAllIndices(sharedIndices, normalizedNames))
        {
            await _localMirror.UpsertMarketIndicesAsync(sharedIndices, cancellationToken);
            return OrderIndices(sharedIndices);
        }

        var fallbackIndices = await _fallbackProvider.GetMarketIndicesAsync(normalizedNames, cancellationToken);
        await _sharedStateStore.UpsertMarketIndicesAsync(fallbackIndices, cancellationToken);
        await _localMirror.UpsertMarketIndicesAsync(fallbackIndices, cancellationToken);
        return OrderIndices(fallbackIndices);
    }

    public async Task<IReadOnlyList<OhlcBarDto>> GetIndexOhlcAsync(
        string indexName,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var normalizedIndexName = NormalizeToken(indexName, string.Empty);
        var normalizedResolution = NormalizeToken(resolution, "1");

        if (await _localMirror.HasIndexOhlcCoverageUntilAsync(normalizedIndexName, normalizedResolution, from, to, cancellationToken))
        {
            return await _localMirror.GetIndexOhlcBarsAsync(
                normalizedIndexName,
                normalizedResolution,
                from,
                to,
                cancellationToken);
        }

        if (await _sharedStateStore.HasIndexOhlcCoverageUntilAsync(normalizedIndexName, normalizedResolution, from, to, cancellationToken))
        {
            var sharedBars = await _sharedStateStore.GetIndexOhlcBarsAsync(
                normalizedIndexName,
                normalizedResolution,
                from,
                to,
                cancellationToken);
            await _localMirror.UpsertIndexOhlcBarsAsync(
                normalizedIndexName,
                normalizedResolution,
                from,
                to,
                sharedBars,
                cancellationToken);
            return sharedBars;
        }

        var fallbackBars = await _fallbackProvider.GetIndexOhlcAsync(
            normalizedIndexName,
            normalizedResolution,
            from,
            to,
            cancellationToken);
        await _sharedStateStore.UpsertIndexOhlcBarsAsync(
            normalizedIndexName,
            normalizedResolution,
            from,
            to,
            fallbackBars,
            cancellationToken);
        await _localMirror.UpsertIndexOhlcBarsAsync(
            normalizedIndexName,
            normalizedResolution,
            from,
            to,
            fallbackBars,
            cancellationToken);

        return fallbackBars;
    }

    public async Task<IReadOnlyList<MarketTradeDto>> GetLatestTradesAsync(
        string symbol,
        string boardId,
        int limit,
        CancellationToken cancellationToken)
    {
        var normalizedSymbol = NormalizeToken(symbol, string.Empty);
        var normalizedBoardId = NormalizeToken(boardId, MockMarketDataProvider.DefaultBoardId);
        var normalizedLimit = Math.Clamp(limit, 1, 200);

        var localTrades = await _localMirror.GetLatestTradesAsync(normalizedBoardId, normalizedSymbol, normalizedLimit, cancellationToken);
        if (localTrades.Count > 0)
        {
            return localTrades;
        }

        var sharedTrades = await _sharedStateStore.GetLatestTradesAsync(normalizedBoardId, normalizedSymbol, normalizedLimit, cancellationToken);
        if (sharedTrades.Count > 0)
        {
            foreach (var trade in sharedTrades)
            {
                await _localMirror.ApplyTradeUpdateAsync(ToUpdate(trade), cancellationToken);
            }

            return sharedTrades;
        }

        var fallbackTrades = await _fallbackProvider.GetLatestTradesAsync(normalizedSymbol, normalizedBoardId, normalizedLimit, cancellationToken);
        foreach (var trade in fallbackTrades)
        {
            var update = ToUpdate(trade);
            await _sharedStateStore.ApplyTradeUpdateAsync(update, cancellationToken);
            await _localMirror.ApplyTradeUpdateAsync(update, cancellationToken);
        }

        return fallbackTrades;
    }

    private static MarketTradeUpdateDto ToUpdate(MarketTradeDto trade)
    {
        return new MarketTradeUpdateDto(
            trade.Symbol,
            trade.BoardId,
            trade.Time,
            trade.Price,
            trade.Change,
            trade.ChangePercent,
            trade.Quantity,
            trade.TotalVolume,
            trade.TotalValue,
            trade.Side);
    }

    private static MarketQuoteDto CreateQuoteFromDetail(SymbolDetailDto detail)
    {
        return new MarketQuoteDto(
            detail.Symbol,
            detail.BoardId,
            detail.MarketId,
            detail.DisplayName,
            detail.ReferencePrice,
            detail.CeilingPrice,
            detail.FloorPrice,
            detail.LastPrice,
            detail.Change,
            detail.ChangePercent,
            detail.LastQuantity,
            detail.TotalVolume,
            detail.TotalValue,
            detail.ForeignBuyVolume,
            detail.ForeignSellVolume,
            detail.ForeignRoom,
            detail.OpenPrice,
            detail.HighPrice,
            detail.LowPrice,
            detail.BidLevels,
            detail.AskLevels,
            detail.TradingStatus,
            detail.UpdatedAt);
    }

    private async Task<SymbolDetailDto?> BackfillSymbolMetadataAsync(
        MarketQuoteDto quote,
        CancellationToken cancellationToken)
    {
        if (_metadataProvider is null)
        {
            return null;
        }

        var metadata = await _metadataProvider.GetSymbolMetadataAsync(
            quote.Symbol,
            quote.BoardId,
            cancellationToken);
        if (metadata is null)
        {
            return null;
        }

        var metadataDetail = CreateDetailFromMetadata(metadata, quote);
        await _sharedStateStore.UpsertSymbolDetailAsync(metadataDetail, cancellationToken);
        await _localMirror.UpsertSymbolDetailAsync(metadataDetail, cancellationToken);
        return metadataDetail;
    }

    private static SymbolDetailDto CreateDetailFromMetadata(SymbolMetadataDto metadata, MarketQuoteDto quote)
    {
        return new SymbolDetailDto(
            metadata.Symbol,
            metadata.BoardId,
            metadata.MarketId,
            metadata.DisplayName,
            metadata.Name,
            metadata.SecurityType,
            metadata.Isin,
            metadata.ProductGroupId,
            metadata.SecurityGroupId,
            quote.ReferencePrice,
            quote.CeilingPrice,
            quote.FloorPrice,
            quote.LastPrice,
            quote.Change,
            quote.ChangePercent,
            quote.LastQuantity,
            quote.TotalVolume,
            quote.TotalValue,
            quote.ForeignBuyVolume,
            quote.ForeignSellVolume,
            quote.ForeignRoom,
            quote.OpenPrice,
            quote.HighPrice,
            quote.LowPrice,
            quote.BidLevels,
            quote.AskLevels,
            string.IsNullOrWhiteSpace(quote.TradingStatus) ? metadata.TradingStatus : quote.TradingStatus,
            metadata.SymbolAdminStatus,
            metadata.TradingMethodStatus,
            metadata.TradingSanctionStatus,
            metadata.ListingDate,
            metadata.FinalTradeDate,
            metadata.OpenInterestQuantity,
            MaxUpdatedAt(quote.UpdatedAt, metadata.UpdatedAt));
    }

    private static SymbolDetailDto CreateDetailFromQuote(MarketQuoteDto quote, SymbolDetailDto? metadata = null)
    {
        return new SymbolDetailDto(
            quote.Symbol,
            quote.BoardId,
            FirstNonEmpty(metadata?.MarketId, quote.MarketId),
            FirstNonEmpty(metadata?.DisplayName, quote.DisplayName),
            FirstNonEmpty(metadata?.Name, quote.DisplayName),
            FirstNonEmpty(metadata?.SecurityType, string.Empty),
            FirstNonEmpty(metadata?.Isin, string.Empty),
            FirstNonEmpty(metadata?.ProductGroupId, string.Empty),
            FirstNonEmpty(metadata?.SecurityGroupId, string.Empty),
            quote.ReferencePrice,
            quote.CeilingPrice,
            quote.FloorPrice,
            quote.LastPrice,
            quote.Change,
            quote.ChangePercent,
            quote.LastQuantity,
            quote.TotalVolume,
            quote.TotalValue,
            quote.ForeignBuyVolume,
            quote.ForeignSellVolume,
            quote.ForeignRoom,
            quote.OpenPrice,
            quote.HighPrice,
            quote.LowPrice,
            quote.BidLevels,
            quote.AskLevels,
            FirstNonEmpty(quote.TradingStatus, metadata?.TradingStatus ?? string.Empty),
            FirstNonEmpty(metadata?.SymbolAdminStatus, string.Empty),
            FirstNonEmpty(metadata?.TradingMethodStatus, string.Empty),
            FirstNonEmpty(metadata?.TradingSanctionStatus, string.Empty),
            metadata?.ListingDate,
            metadata?.FinalTradeDate,
            metadata?.OpenInterestQuantity ?? 0,
            metadata is null ? quote.UpdatedAt : MaxUpdatedAt(quote.UpdatedAt, metadata.UpdatedAt));
    }

    private async Task<IReadOnlyCollection<string>> ResolveRequestedSymbolsAsync(
        MarketBoardQuery query,
        CancellationToken cancellationToken)
    {
        var explicitSymbols = NormalizeSymbols(query.Symbols);
        if (explicitSymbols.Count > 0)
        {
            return explicitSymbols;
        }

        var localSymbols = await _localMirror.GetSymbolMembershipsAsync(query, cancellationToken);
        if (localSymbols.Count > 0)
        {
            return NormalizeSymbols(localSymbols);
        }

        var sharedSymbols = await _sharedStateStore.GetSymbolMembershipsAsync(query, cancellationToken);
        if (sharedSymbols.Count > 0)
        {
            await _localMirror.UpsertSymbolMembershipsAsync(query, sharedSymbols, cancellationToken);
            return NormalizeSymbols(sharedSymbols);
        }

        return [];
    }

    private async Task StoreFallbackQuotesAsync(
        MarketBoardQuery query,
        IReadOnlyCollection<string> requestedSymbols,
        IReadOnlyCollection<MarketQuoteDto> fallbackQuotes,
        CancellationToken cancellationToken)
    {
        await _sharedStateStore.UpsertQuotesAsync(fallbackQuotes, cancellationToken);
        await _localMirror.UpsertQuotesAsync(fallbackQuotes, cancellationToken);

        var membershipSymbols = requestedSymbols.Count > 0
            ? requestedSymbols
                .Concat(fallbackQuotes.Select(quote => quote.Symbol))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
            : fallbackQuotes.Select(quote => quote.Symbol).ToArray();

        await _sharedStateStore.UpsertSymbolMembershipsAsync(query, membershipSymbols, cancellationToken);
        await _localMirror.UpsertSymbolMembershipsAsync(query, membershipSymbols, cancellationToken);
    }

    private static bool IsUsableQuote(MarketQuoteDto quote)
    {
        return quote.ReferencePrice > 0m && quote.CeilingPrice > 0m && quote.FloorPrice > 0m;
    }

    private static bool IsUsableDetail(SymbolDetailDto detail)
    {
        return detail.ReferencePrice > 0m && detail.CeilingPrice > 0m && detail.FloorPrice > 0m;
    }

    private static string FirstNonEmpty(string? primary, string fallback)
    {
        return string.IsNullOrWhiteSpace(primary) ? fallback : primary;
    }

    private static DateTimeOffset MaxUpdatedAt(DateTimeOffset left, DateTimeOffset right)
    {
        return left >= right ? left : right;
    }

    private static bool HasAllIndices(IReadOnlyCollection<MarketIndexDto> indices, IReadOnlyCollection<string> indexNames)
    {
        var availableNames = indices.Select(index => index.IndexName).ToHashSet(StringComparer.Ordinal);
        return indexNames.All(availableNames.Contains);
    }

    private static IReadOnlyList<MarketQuoteDto> OrderQuotes(IEnumerable<MarketQuoteDto> quotes)
    {
        return quotes.OrderBy(quote => quote.Symbol, StringComparer.Ordinal).ToArray();
    }

    private static void AddUsableQuotes(
        IDictionary<string, MarketQuoteDto> quotesBySymbol,
        IEnumerable<MarketQuoteDto> quotes)
    {
        foreach (var quote in quotes.Where(IsUsableQuote))
        {
            quotesBySymbol[quote.Symbol] = quote;
        }
    }

    private static IReadOnlyCollection<string> MissingSymbols(
        IReadOnlyCollection<string> requestedSymbols,
        IReadOnlyDictionary<string, MarketQuoteDto> quotesBySymbol)
    {
        return requestedSymbols
            .Where(symbol => !quotesBySymbol.ContainsKey(symbol))
            .ToArray();
    }

    private static string FormatSymbols(IReadOnlyCollection<string> symbols)
    {
        const int maxSymbolsToLog = 20;
        var visibleSymbols = symbols.Take(maxSymbolsToLog).ToArray();
        var suffix = symbols.Count > maxSymbolsToLog ? $", +{symbols.Count - maxSymbolsToLog} more" : string.Empty;
        return $"{string.Join(", ", visibleSymbols)}{suffix}";
    }

    private static IReadOnlyList<MarketIndexDto> OrderIndices(IEnumerable<MarketIndexDto> indices)
    {
        return indices.OrderBy(index => index.IndexName, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyCollection<string> NormalizeSymbols(IReadOnlyCollection<string> symbols)
    {
        return symbols
            .SelectMany(symbol => symbol.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(symbol => NormalizeToken(symbol, string.Empty))
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeToken(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToUpperInvariant();
    }
}
