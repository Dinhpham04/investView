using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Dtos.MarketData;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestView.Infrastructure.MarketData;

public sealed class CachedMarketDataProvider : IMarketDataProvider
{
    private readonly IMarketDataProvider _inner;
    private readonly IMemoryCache _cache;
    private readonly MarketDataCacheOptions _options;
    private readonly ILogger<CachedMarketDataProvider> _logger;

    public CachedMarketDataProvider(
        IMarketDataProvider inner,
        IMemoryCache cache,
        IOptions<MarketDataCacheOptions> options,
        ILogger<CachedMarketDataProvider> logger)
    {
        _inner = inner;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MarketQuoteDto>> GetMarketBoardAsync(
        MarketBoardQuery query,
        CancellationToken cancellationToken)
    {
        var normalizedSymbols = NormalizeSymbols(query.Symbols);
        var normalizedBoardId = NormalizeToken(query.BoardId, MockMarketDataProvider.DefaultBoardId);
        var normalizedMarketId = NormalizeToken(query.MarketId ?? string.Empty, string.Empty);
        var normalizedIndexName = NormalizeToken(query.IndexName ?? string.Empty, string.Empty);
        var normalizedQuery = new MarketBoardQuery(
            normalizedSymbols,
            normalizedBoardId,
            normalizedMarketId,
            normalizedIndexName);
        var cacheKey = $"market-board:{normalizedBoardId}:{normalizedMarketId}:{normalizedIndexName}:{string.Join(',', normalizedSymbols)}";

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<MarketQuoteDto>? cachedQuotes) && cachedQuotes is not null)
        {
            _logger.LogDebug("Market data cache hit for {CacheKey}.", cacheKey);
            return cachedQuotes;
        }

        _logger.LogDebug("Market data cache miss for {CacheKey}.", cacheKey);
        var quotes = await _inner.GetMarketBoardAsync(normalizedQuery, cancellationToken);
        _cache.Set(cacheKey, quotes, _options.MarketBoardTtl);

        return quotes;
    }

    public async Task<SymbolDetailDto?> GetSymbolDetailAsync(
        string symbol,
        CancellationToken cancellationToken)
    {
        var normalizedSymbol = NormalizeToken(symbol, string.Empty);
        var cacheKey = $"symbol-detail:{normalizedSymbol}";

        if (_cache.TryGetValue(cacheKey, out SymbolDetailDto? cachedDetail) && cachedDetail is not null)
        {
            _logger.LogDebug("Market data cache hit for {CacheKey}.", cacheKey);
            return cachedDetail;
        }

        _logger.LogDebug("Market data cache miss for {CacheKey}.", cacheKey);
        var detail = await _inner.GetSymbolDetailAsync(normalizedSymbol, cancellationToken);
        if (detail is not null)
        {
            _cache.Set(cacheKey, detail, _options.SymbolDetailTtl);
        }

        return detail;
    }

    public async Task<IReadOnlyList<OhlcBarDto>> GetOhlcAsync(
        string symbol,
        string resolution,
        CancellationToken cancellationToken)
    {
        var normalizedSymbol = NormalizeToken(symbol, string.Empty);
        var normalizedResolution = NormalizeToken(resolution, string.Empty);
        var cacheKey = $"ohlc:{normalizedSymbol}:{normalizedResolution}";

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<OhlcBarDto>? cachedBars) && cachedBars is not null)
        {
            _logger.LogDebug("Market data cache hit for {CacheKey}.", cacheKey);
            return cachedBars;
        }

        _logger.LogDebug("Market data cache miss for {CacheKey}.", cacheKey);
        var bars = await _inner.GetOhlcAsync(normalizedSymbol, normalizedResolution, cancellationToken);
        _cache.Set(cacheKey, bars, _options.OhlcTtl);

        return bars;
    }

    private static string NormalizeToken(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim().ToUpperInvariant();
    }

    private static IReadOnlyCollection<string> NormalizeSymbols(IReadOnlyCollection<string> symbols)
    {
        return symbols
            .SelectMany(symbol => symbol.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(symbol => symbol.Trim().ToUpperInvariant())
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
