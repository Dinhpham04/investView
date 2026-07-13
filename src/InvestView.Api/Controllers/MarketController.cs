using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Dtos.MarketData;
using InvestView.Infrastructure.MarketData;
using Microsoft.AspNetCore.Mvc;

namespace InvestView.Api.Controllers;

[ApiController]
[Route("api/market")]
[Produces("application/json")]
public sealed class MarketController : ControllerBase
{
    private const string DefaultBoardId = "G1";
    private const string DefaultMarketId = "HOSE";
    private const string DefaultProductGroupId = "STO";
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly IMarketStateStore _marketStateStore;
    private readonly TimeProvider _timeProvider;

    public MarketController(
        IMarketDataProvider marketDataProvider,
        IMarketStateStore marketStateStore,
        TimeProvider timeProvider)
    {
        _marketDataProvider = marketDataProvider;
        _marketStateStore = marketStateStore;
        _timeProvider = timeProvider;
    }

    [HttpGet("quotes")]
    [ProducesResponseType<IReadOnlyList<MarketQuoteDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MarketQuoteDto>>> GetQuotes(
        [FromQuery] string[]? symbols,
        [FromQuery] string? boardId,
        [FromQuery] string? marketId,
        [FromQuery] string? indexName,
        CancellationToken cancellationToken)
    {
        var quotes = await _marketDataProvider.GetMarketBoardAsync(
            new MarketBoardQuery(
                symbols ?? [],
                string.IsNullOrWhiteSpace(boardId) ? DefaultBoardId : boardId,
                marketId,
                indexName),
            cancellationToken);

        return Ok(quotes);
    }

    [HttpGet("session")]
    [ProducesResponseType<MarketSessionUpdateDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MarketSessionUpdateDto>> GetMarketSession(
        [FromQuery] string? productGroupId,
        [FromQuery] string? boardId,
        [FromQuery] string? marketId,
        CancellationToken cancellationToken)
    {
        var normalizedProductGroupId = string.IsNullOrWhiteSpace(productGroupId)
            ? DefaultProductGroupId
            : productGroupId.Trim().ToUpperInvariant();
        var normalizedBoardId = string.IsNullOrWhiteSpace(boardId)
            ? DefaultBoardId
            : boardId.Trim().ToUpperInvariant();
        var normalizedMarketId = string.IsNullOrWhiteSpace(marketId)
            ? DefaultMarketId
            : marketId.Trim().ToUpperInvariant();
        var now = _timeProvider.GetUtcNow();

        var cachedSession = await _marketStateStore.GetMarketSessionAsync(
            normalizedProductGroupId,
            normalizedBoardId,
            cancellationToken);
        if (cachedSession is not null)
        {
            return Ok(MarketSessionResolver.Resolve(cachedSession, now));
        }

        var fallbackSession = new MarketSessionUpdateDto(
            normalizedMarketId,
            normalizedBoardId,
            normalizedProductGroupId,
            EventId: string.Empty,
            TradingSessionId: string.Empty,
            UpdatedAt: now);

        return Ok(MarketSessionResolver.Resolve(fallbackSession));
    }

    [HttpGet("symbols/{symbol}")]
    [ProducesResponseType<SymbolDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SymbolDetailDto>> GetSymbolDetail(
        string symbol,
        [FromQuery] string? boardId,
        CancellationToken cancellationToken)
    {
        var detail = await _marketDataProvider.GetSymbolDetailAsync(
            symbol,
            string.IsNullOrWhiteSpace(boardId) ? DefaultBoardId : boardId,
            cancellationToken);

        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpGet("symbols/{symbol}/ohlc")]
    [ProducesResponseType<IReadOnlyList<OhlcBarDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OhlcBarDto>>> GetOhlc(
        string symbol,
        [FromQuery] string? resolution,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var bars = await _marketDataProvider.GetOhlcAsync(
            symbol,
            string.IsNullOrWhiteSpace(resolution) ? "1" : resolution,
            from,
            to,
            cancellationToken);

        return Ok(bars);
    }

    [HttpGet("indices")]
    [ProducesResponseType<IReadOnlyList<MarketIndexDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MarketIndexDto>>> GetMarketIndices(
        [FromQuery] string[]? names,
        CancellationToken cancellationToken)
    {
        var indices = await _marketDataProvider.GetMarketIndicesAsync(names ?? [], cancellationToken);
        return Ok(indices);
    }

    [HttpGet("indices/{indexName}/ohlc")]
    [ProducesResponseType<IReadOnlyList<OhlcBarDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OhlcBarDto>>> GetIndexOhlc(
        string indexName,
        [FromQuery] string? resolution,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var bars = await _marketDataProvider.GetIndexOhlcAsync(
            indexName,
            string.IsNullOrWhiteSpace(resolution) ? "1" : resolution,
            from,
            to,
            cancellationToken);

        return Ok(bars);
    }

    [HttpGet("symbols/{symbol}/trades/latest")]
    [ProducesResponseType<IReadOnlyList<MarketTradeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MarketTradeDto>>> GetLatestTrades(
        string symbol,
        [FromQuery] string? boardId,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var trades = await _marketDataProvider.GetLatestTradesAsync(
            symbol,
            string.IsNullOrWhiteSpace(boardId) ? DefaultBoardId : boardId,
            Math.Clamp(limit ?? 50, 1, 200),
            cancellationToken);

        return Ok(trades);
    }
}
