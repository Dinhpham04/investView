namespace InvestView.Application.Abstractions.MarketData;

public interface IMarketDataStream
{
    Task SubscribeAsync(
        IReadOnlyCollection<string> symbols,
        string boardId,
        IReadOnlyCollection<MarketDataChannel> channels,
        CancellationToken cancellationToken);
}
