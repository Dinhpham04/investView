using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Dtos.MarketData;
using Microsoft.AspNetCore.Mvc;

namespace InvestView.Api.Controllers;

[ApiController]
[Route("api/market")]
[Produces("application/json")]
public sealed class MarketController : ControllerBase
{
    private const string DefaultBoardId = "G1";
    private readonly IMarketDataProvider _marketDataProvider;

    public MarketController(IMarketDataProvider marketDataProvider)
    {
        _marketDataProvider = marketDataProvider;
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
