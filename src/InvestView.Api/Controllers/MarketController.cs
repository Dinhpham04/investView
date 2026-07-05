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
        CancellationToken cancellationToken)
    {
        var quotes = await _marketDataProvider.GetMarketBoardAsync(
            symbols ?? [],
            string.IsNullOrWhiteSpace(boardId) ? DefaultBoardId : boardId,
            cancellationToken);

        return Ok(quotes);
    }
}
