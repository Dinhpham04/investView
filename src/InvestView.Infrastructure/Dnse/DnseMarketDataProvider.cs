using System.Text.Json;
using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Dtos.MarketData;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InvestView.Infrastructure.Dnse;

public sealed class DnseMarketDataProvider : IMarketDataProvider
{
    private const string DefaultBoardId = "G1";
    private readonly IDnseMarketDataClient _client;
    private readonly ILogger<DnseMarketDataProvider> _logger;
    private readonly DnseMarketDataOptions _options;

    public DnseMarketDataProvider(
        IDnseMarketDataClient client,
        IOptions<DnseMarketDataOptions> options,
        ILogger<DnseMarketDataProvider>? logger = null)
    {
        _client = client;
        _options = options.Value;
        _logger = logger ?? NullLogger<DnseMarketDataProvider>.Instance;
    }

    public async Task<IReadOnlyList<MarketQuoteDto>> GetMarketBoardAsync(
        MarketBoardQuery query,
        CancellationToken cancellationToken)
    {
        var normalizedBoardId = NormalizeBoardId(query.BoardId);
        using var instruments = await GetInstrumentsAsync(query, cancellationToken);
        var normalizedSymbols = ResolveSymbols(query, instruments.RootElement);
        if (normalizedSymbols.Count == 0)
        {
            return [];
        }

        var quoteTasks = normalizedSymbols.Select(symbol =>
            GetMarketQuoteAsync(symbol, normalizedBoardId, instruments.RootElement, cancellationToken));

        var quotes = await Task.WhenAll(quoteTasks);
        return quotes.OrderBy(quote => quote.Symbol, StringComparer.Ordinal).ToArray();
    }

    public async Task<SymbolDetailDto?> GetSymbolDetailAsync(
        string symbol,
        string boardId,
        CancellationToken cancellationToken)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        var normalizedBoardId = NormalizeBoardId(boardId);
        if (string.IsNullOrWhiteSpace(normalizedSymbol))
        {
            return null;
        }

        using var instruments = await _client.GetJsonAsync(
            "/instruments",
            new Dictionary<string, string?>
            {
                ["symbol"] = normalizedSymbol,
                ["limit"] = "1",
                ["page"] = "1"
            },
            cancellationToken);
        using var securityDefinition = await _client.GetJsonAsync(
            $"/price/{normalizedSymbol}/secdef",
            new Dictionary<string, string?> { ["boardId"] = normalizedBoardId },
            cancellationToken);
        using var latestTrade = await _client.GetJsonAsync(
            $"/price/{normalizedSymbol}/trades/latest",
            new Dictionary<string, string?> { ["boardId"] = normalizedBoardId },
            cancellationToken);
        using var latestQuote = await _client.GetJsonAsync(
            $"/price/{normalizedSymbol}/quotes/latest",
            new Dictionary<string, string?> { ["boardId"] = normalizedBoardId },
            cancellationToken);
        using var foreignTrading = await GetForeignTradingAsync(normalizedSymbol, normalizedBoardId, cancellationToken);

        var instrument = DnseMarketDataMapper.FindObjectBySymbol(instruments.RootElement, normalizedSymbol);
        return DnseMarketDataMapper.MapSymbolDetail(
            normalizedSymbol,
            normalizedBoardId,
            instrument,
            securityDefinition.RootElement,
            latestTrade.RootElement,
            latestQuote.RootElement,
            foreignTrading.RootElement,
            DateTimeOffset.UtcNow,
            _options.QuantityScaleFactor);
    }

    public async Task<IReadOnlyList<OhlcBarDto>> GetOhlcAsync(
        string symbol,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        var normalizedResolution = NormalizeResolution(resolution);
        if (string.IsNullOrWhiteSpace(normalizedSymbol))
        {
            return [];
        }

        return await GetOhlcAsync("STOCK", normalizedSymbol, normalizedResolution, from, to, cancellationToken);
    }

    public async Task<IReadOnlyList<MarketIndexDto>> GetMarketIndicesAsync(
        IReadOnlyCollection<string> indexNames,
        CancellationToken cancellationToken)
    {
        var normalizedIndexNames = NormalizeSymbols(indexNames.Count > 0 ? indexNames : _options.DefaultMarketIndices);
        if (normalizedIndexNames.Count == 0)
        {
            return [];
        }

        var indexTasks = normalizedIndexNames.Select(indexName => GetMarketIndexAsync(indexName, cancellationToken));
        var indices = await Task.WhenAll(indexTasks);
        return indices
            .Where(index => index is not null)
            .Select(index => index!)
            .OrderBy(index => index.IndexName, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IReadOnlyList<OhlcBarDto>> GetIndexOhlcAsync(
        string indexName,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var normalizedIndexName = NormalizeSymbol(indexName);
        var normalizedResolution = NormalizeResolution(resolution);
        if (string.IsNullOrWhiteSpace(normalizedIndexName))
        {
            return [];
        }

        return await GetOhlcAsync("INDEX", normalizedIndexName, normalizedResolution, from, to, cancellationToken);
    }

    private async Task<IReadOnlyList<OhlcBarDto>> GetOhlcAsync(
        string type,
        string symbol,
        string resolution,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        using var ohlc = await _client.GetJsonAsync(
            "/price/ohlc",
            new Dictionary<string, string?>
            {
                ["type"] = type,
                ["symbol"] = symbol,
                ["resolution"] = resolution,
                ["from"] = from?.ToUnixTimeSeconds().ToString(),
                ["to"] = to?.ToUnixTimeSeconds().ToString()
            },
            cancellationToken);

        return DnseMarketDataMapper.MapOhlcBars(
            symbol,
            resolution,
            ohlc.RootElement,
            _options.QuantityScaleFactor,
            type.Equals("INDEX", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<MarketIndexDto?> GetMarketIndexAsync(
        string indexName,
        CancellationToken cancellationToken)
    {
        var to = DateTimeOffset.UtcNow;
        var from = to.AddDays(-5);
        var bars = await GetIndexOhlcAsync(indexName, "1D", from, to, cancellationToken);
        var orderedBars = bars.OrderBy(bar => bar.Time).ToArray();
        var latestBar = orderedBars.LastOrDefault();
        if (latestBar is null)
        {
            return null;
        }

        var previousBar = orderedBars.Length > 1 ? orderedBars[^2] : null;
        var referenceValue = previousBar?.Close ?? latestBar.Open;
        var change = latestBar.Close - referenceValue;
        var changePercent = referenceValue == 0m ? 0m : change / referenceValue * 100m;

        return new MarketIndexDto(
            IndexName: indexName,
            Value: latestBar.Close,
            Change: change,
            ChangePercent: changePercent,
            ReferenceValue: referenceValue,
            HighValue: latestBar.High,
            LowValue: latestBar.Low,
            TotalVolume: latestBar.Volume,
            TotalValue: null,
            UpCount: null,
            DownCount: null,
            NoChangeCount: null,
            CeilingCount: null,
            FloorCount: null,
            MarketId: string.Empty,
            TradingSessionId: string.Empty,
            UpdatedAt: latestBar.Time);
    }

    public async Task<IReadOnlyList<MarketTradeDto>> GetLatestTradesAsync(
        string symbol,
        string boardId,
        int limit,
        CancellationToken cancellationToken)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        var normalizedBoardId = NormalizeBoardId(boardId);
        if (string.IsNullOrWhiteSpace(normalizedSymbol))
        {
            return [];
        }

        var normalizedLimit = Math.Clamp(limit, 1, 200);
        var to = DateTimeOffset.UtcNow;
        var lookbackHours = Math.Max(_options.LatestTradesLookbackHours, 1);
        var from = to.AddHours(-lookbackHours);
        using var trades = await _client.GetJsonAsync(
            $"/price/{normalizedSymbol}/trades",
            new Dictionary<string, string?>
            {
                ["boardId"] = normalizedBoardId,
                ["from"] = from.ToUnixTimeSeconds().ToString(),
                ["to"] = to.ToUnixTimeSeconds().ToString(),
                ["limit"] = normalizedLimit.ToString(),
                ["order"] = "DESC"
            },
            cancellationToken);

        return DnseMarketDataMapper.MapLatestTrades(
            normalizedSymbol,
            normalizedBoardId,
            trades.RootElement,
            DateTimeOffset.UtcNow,
            _options.QuantityScaleFactor);
    }

    private async Task<MarketQuoteDto> GetMarketQuoteAsync(
        string symbol,
        string boardId,
        JsonElement instruments,
        CancellationToken cancellationToken)
    {
        var instrument = DnseMarketDataMapper.FindObjectBySymbol(instruments, symbol);

        using var securityDefinition = await _client.GetJsonAsync(
            $"/price/{symbol}/secdef",
            new Dictionary<string, string?> { ["boardId"] = boardId },
            cancellationToken);
        using var latestTrade = await _client.GetJsonAsync(
            $"/price/{symbol}/trades/latest",
            new Dictionary<string, string?> { ["boardId"] = boardId },
            cancellationToken);
        using var latestQuote = await _client.GetJsonAsync(
            $"/price/{symbol}/quotes/latest",
            new Dictionary<string, string?> { ["boardId"] = boardId },
            cancellationToken);
        using var foreignTrading = await GetForeignTradingAsync(symbol, boardId, cancellationToken);

        var marketQuote = DnseMarketDataMapper.MapMarketQuote(
            symbol,
            boardId,
            instrument,
            securityDefinition.RootElement,
            latestTrade.RootElement,
            latestQuote.RootElement,
            foreignTrading.RootElement,
            DateTimeOffset.UtcNow,
            _options.QuantityScaleFactor);

        _logger.LogInformation(
            "DNSE mapped quote {Symbol}: last={LastPrice}, ref={ReferencePrice}, change={Change}, bidLevels={BidLevelCount}, askLevels={AskLevelCount}, totalVolume={TotalVolume}, foreignBuy={ForeignBuyVolume}, foreignSell={ForeignSellVolume}, updatedAt={UpdatedAt}",
            marketQuote.Symbol,
            marketQuote.LastPrice,
            marketQuote.ReferencePrice,
            marketQuote.Change,
            marketQuote.BidLevels.Count,
            marketQuote.AskLevels.Count,
            marketQuote.TotalVolume,
            marketQuote.ForeignBuyVolume,
            marketQuote.ForeignSellVolume,
            marketQuote.UpdatedAt);

        return marketQuote;
    }

    private Task<JsonDocument> GetInstrumentsAsync(
        MarketBoardQuery query,
        CancellationToken cancellationToken)
    {
        var explicitSymbols = NormalizeSymbols(query.Symbols);
        var marketId = NormalizeToken(query.MarketId);
        var indexName = NormalizeToken(query.IndexName);
        var hasFilter = !string.IsNullOrWhiteSpace(marketId) || !string.IsNullOrWhiteSpace(indexName);
        var symbols = explicitSymbols.Count > 0
            ? explicitSymbols
            : hasFilter
                ? []
                : NormalizeSymbols(_options.DefaultSymbols);

        if (symbols.Count == 0 && hasFilter)
        {
            return GetPagedInstrumentsAsync(marketId, indexName, cancellationToken);
        }

        return GetInstrumentPageAsync(
            symbols.Count > 0 ? string.Join(',', symbols) : null,
            marketId,
            hasFilter,
            indexName,
            Math.Max(symbols.Count, 1),
            1,
            cancellationToken);
    }

    private async Task<JsonDocument> GetPagedInstrumentsAsync(
        string marketId,
        string indexName,
        CancellationToken cancellationToken)
    {
        var pageSize = Math.Max(_options.InstrumentPageSize, 1);
        var maxPages = Math.Max(_options.MaxInstrumentPages, 1);
        var instrumentPayloads = new List<string>();

        for (var page = 1; page <= maxPages; page++)
        {
            using var instruments = await GetInstrumentPageAsync(
                null,
                marketId,
                hasFilter: true,
                indexName,
                pageSize,
                page,
                cancellationToken);
            var pageSymbols = DnseMarketDataMapper.ExtractInstrumentSymbols(instruments.RootElement, pageSize);
            instrumentPayloads.AddRange(DnseMarketDataMapper.ExtractInstrumentPayloads(instruments.RootElement));

            if (pageSymbols.Count < pageSize)
            {
                break;
            }
        }

        return JsonDocument.Parse($$"""{"data":[{{string.Join(',', instrumentPayloads)}}]}""");
    }

    private Task<JsonDocument> GetInstrumentPageAsync(
        string? symbols,
        string marketId,
        bool hasFilter,
        string indexName,
        int limit,
        int page,
        CancellationToken cancellationToken)
    {
        return _client.GetJsonAsync(
            "/instruments",
            new Dictionary<string, string?>
            {
                ["symbol"] = symbols,
                ["marketId"] = string.IsNullOrWhiteSpace(marketId) ? null : marketId,
                ["securityGroupId"] = hasFilter ? "ST" : null,
                ["indexName"] = string.IsNullOrWhiteSpace(indexName) ? null : indexName,
                ["limit"] = limit.ToString(),
                ["page"] = page.ToString()
            },
            cancellationToken);
    }

    private IReadOnlyCollection<string> ResolveSymbols(
        MarketBoardQuery query,
        JsonElement instruments)
    {
        var explicitSymbols = NormalizeSymbols(query.Symbols);
        if (explicitSymbols.Count > 0)
        {
            return explicitSymbols;
        }

        var marketId = NormalizeToken(query.MarketId);
        var indexName = NormalizeToken(query.IndexName);
        if (!string.IsNullOrWhiteSpace(marketId) || !string.IsNullOrWhiteSpace(indexName))
        {
            return DnseMarketDataMapper.ExtractInstrumentSymbols(instruments, int.MaxValue);
        }

        return NormalizeSymbols(_options.DefaultSymbols);
    }

    private async Task<JsonDocument> GetForeignTradingAsync(
        string symbol,
        string boardId,
        CancellationToken cancellationToken)
    {
        var to = DateTimeOffset.UtcNow;
        var lookbackHours = Math.Max(_options.ForeignTradingLookbackHours, 1);
        var from = to.AddHours(-lookbackHours);

        try
        {
            return await _client.GetJsonAsync(
                $"/price/{symbol}/foreign-trading",
                new Dictionary<string, string?>
                {
                    ["boardId"] = boardId,
                    ["from"] = from.ToUnixTimeSeconds().ToString(),
                    ["to"] = to.ToUnixTimeSeconds().ToString(),
                    ["limit"] = "1",
                    ["order"] = "DESC"
                },
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "DNSE foreign trading snapshot failed for {Symbol}. Market board will continue without foreign trading data.",
                symbol);

            return JsonDocument.Parse("{}");
        }
    }

    private static IReadOnlyCollection<string> NormalizeSymbols(IReadOnlyCollection<string> symbols)
    {
        return symbols
            .SelectMany(symbol => symbol.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(NormalizeSymbol)
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeBoardId(string boardId)
    {
        return string.IsNullOrWhiteSpace(boardId)
            ? DefaultBoardId
            : boardId.Trim().ToUpperInvariant();
    }

    private static string NormalizeSymbol(string symbol)
    {
        return string.IsNullOrWhiteSpace(symbol)
            ? string.Empty
            : symbol.Trim().ToUpperInvariant();
    }

    private static string NormalizeResolution(string resolution)
    {
        return string.IsNullOrWhiteSpace(resolution)
            ? "1"
            : resolution.Trim().ToUpperInvariant();
    }

    private static string NormalizeToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }
}
