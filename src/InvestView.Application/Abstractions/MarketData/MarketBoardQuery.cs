namespace InvestView.Application.Abstractions.MarketData;

public sealed record MarketBoardQuery(
    IReadOnlyCollection<string> Symbols,
    string BoardId,
    string? MarketId = null,
    string? IndexName = null);
