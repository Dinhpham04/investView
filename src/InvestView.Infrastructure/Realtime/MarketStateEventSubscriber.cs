using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Abstractions.Realtime;
using Microsoft.Extensions.Logging;

namespace InvestView.Infrastructure.Realtime;

public sealed class MarketStateEventSubscriber : IMarketStateEventHandler
{
    private readonly IMarketStateMirror _localMirror;
    private readonly IMarketStateStore _sharedStateStore;
    private readonly IMarketQuoteBroadcaster _broadcaster;
    private readonly ILogger<MarketStateEventSubscriber> _logger;

    public MarketStateEventSubscriber(
        IMarketStateMirror localMirror,
        IMarketStateStore sharedStateStore,
        IMarketQuoteBroadcaster broadcaster,
        ILogger<MarketStateEventSubscriber> logger)
    {
        _localMirror = localMirror;
        _sharedStateStore = sharedStateStore;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public async Task HandleAsync(MarketStateEvent marketEvent, CancellationToken cancellationToken)
    {
        switch (marketEvent.Kind)
        {
            case MarketStateEventKind.QuoteUpdate when marketEvent.QuoteUpdate is not null:
                await WarmQuoteMirrorAsync(
                    marketEvent.QuoteUpdate.BoardId,
                    marketEvent.QuoteUpdate.Symbol,
                    cancellationToken);
                var quoteUpdate = await _localMirror.ApplyQuoteUpdateAsync(marketEvent.QuoteUpdate, cancellationToken);
                await _broadcaster.BroadcastQuoteUpdateAsync(quoteUpdate, cancellationToken);
                break;
            case MarketStateEventKind.TradeUpdate when marketEvent.TradeUpdate is not null:
                await WarmQuoteMirrorAsync(
                    marketEvent.TradeUpdate.BoardId,
                    marketEvent.TradeUpdate.Symbol,
                    cancellationToken);
                var tradeUpdate = await _localMirror.ApplyTradeUpdateAsync(marketEvent.TradeUpdate, cancellationToken);
                await _broadcaster.BroadcastTradeUpdateAsync(tradeUpdate, cancellationToken);
                break;
            case MarketStateEventKind.MarketIndexUpdate when marketEvent.MarketIndexUpdate is not null:
                await WarmIndexMirrorAsync(marketEvent.MarketIndexUpdate.IndexName, cancellationToken);
                var indexUpdate = await _localMirror.ApplyMarketIndexUpdateAsync(marketEvent.MarketIndexUpdate, cancellationToken);
                await _broadcaster.BroadcastMarketIndexUpdateAsync(indexUpdate, cancellationToken);
                break;
            default:
                _logger.LogDebug("Ignored market state event kind {Kind}.", marketEvent.Kind);
                break;
        }
    }

    private async Task WarmQuoteMirrorAsync(string boardId, string symbol, CancellationToken cancellationToken)
    {
        var localQuotes = await _localMirror.GetQuotesAsync(boardId, [symbol], cancellationToken);
        if (localQuotes.Any(IsUsableQuote))
        {
            return;
        }

        var sharedQuotes = await _sharedStateStore.GetQuotesAsync(boardId, [symbol], cancellationToken);
        var usableQuotes = sharedQuotes.Where(IsUsableQuote).ToArray();
        if (usableQuotes.Length > 0)
        {
            await _localMirror.UpsertQuotesAsync(usableQuotes, cancellationToken);
        }
    }

    private async Task WarmIndexMirrorAsync(string indexName, CancellationToken cancellationToken)
    {
        var localIndices = await _localMirror.GetMarketIndicesAsync([indexName], cancellationToken);
        if (localIndices.Count > 0)
        {
            return;
        }

        var sharedIndices = await _sharedStateStore.GetMarketIndicesAsync([indexName], cancellationToken);
        if (sharedIndices.Count > 0)
        {
            await _localMirror.UpsertMarketIndicesAsync(sharedIndices, cancellationToken);
        }
    }

    private static bool IsUsableQuote(InvestView.Application.Dtos.MarketData.MarketQuoteDto quote)
    {
        return quote.ReferencePrice > 0m && quote.CeilingPrice > 0m && quote.FloorPrice > 0m;
    }
}
