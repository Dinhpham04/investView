using InvestView.Application.Dtos.MarketData;

namespace InvestView.Application.Abstractions.MarketData;

public interface IMarketDataProvider
{
    Task<IReadOnlyList<MarketQuoteDto>> GetMarketBoardAsync(
        IReadOnlyCollection<string> symbols,
        string boardId,
        CancellationToken cancellationToken);

    Task<SymbolDetailDto?> GetSymbolDetailAsync(
        string symbol,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OhlcBarDto>> GetOhlcAsync(
        string symbol,
        string resolution,
        CancellationToken cancellationToken);
}
