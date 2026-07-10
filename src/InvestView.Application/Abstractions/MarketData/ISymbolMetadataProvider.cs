using InvestView.Application.Dtos.MarketData;

namespace InvestView.Application.Abstractions.MarketData;

public interface ISymbolMetadataProvider
{
    Task<SymbolMetadataDto?> GetSymbolMetadataAsync(
        string symbol,
        string boardId,
        CancellationToken cancellationToken);
}
