using InvestView.Api.Controllers;
using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Dtos.MarketData;
using InvestView.Infrastructure.MarketData;
using Microsoft.AspNetCore.Mvc;

namespace InvestView.Api.Tests.Controllers;

public sealed class MarketControllerTests
{
    [Fact]
    public async Task GetMarketSession_WhenCacheIsEmpty_ReturnsScheduledFallback()
    {
        var controller = new MarketController(
            new ThrowingMarketDataProvider(),
            new InMemoryMarketStateStore(),
            new FixedTimeProvider(Utc(2026, 7, 13, 2, 5)));

        var result = await controller.GetMarketSession("STO", "G1", "HOSE", CancellationToken.None);

        var session = AssertOkSession(result);
        Assert.Equal(MarketSessionPhases.Ato, session.Phase);
        Assert.Equal(MarketSessionSources.ScheduleFallback, session.Source);
        Assert.True(session.IsAuction);
    }

    [Fact]
    public async Task GetMarketSession_WhenCacheExists_ReturnsResolvedRealtimeSession()
    {
        var stateStore = new InMemoryMarketStateStore();
        await stateStore.ApplyMarketSessionUpdateAsync(
            new MarketSessionUpdateDto(
                MarketId: "HOSE",
                BoardId: "G1",
                ProductGroupId: "STO",
                EventId: "AB2",
                TradingSessionId: "40",
                UpdatedAt: Utc(2026, 7, 13, 2, 20)),
            CancellationToken.None);
        var controller = new MarketController(
            new ThrowingMarketDataProvider(),
            stateStore,
            new FixedTimeProvider(Utc(2026, 7, 13, 2, 21)));

        var result = await controller.GetMarketSession("STO", "G1", "HOSE", CancellationToken.None);

        var session = AssertOkSession(result);
        Assert.Equal(MarketSessionPhases.Continuous, session.Phase);
        Assert.Equal(MarketSessionSources.Realtime, session.Source);
        Assert.True(session.IsContinuous);
    }

    [Fact]
    public async Task GetMarketSession_WhenCachedContinuousSessionCrossesLunchBoundary_ReturnsScheduledLunchBreak()
    {
        var stateStore = new InMemoryMarketStateStore();
        await stateStore.ApplyMarketSessionUpdateAsync(
            new MarketSessionUpdateDto(
                MarketId: "HOSE",
                BoardId: "G1",
                ProductGroupId: "STO",
                EventId: "AB2",
                TradingSessionId: "40",
                UpdatedAt: Utc(2026, 7, 13, 2, 20)),
            CancellationToken.None);
        var controller = new MarketController(
            new ThrowingMarketDataProvider(),
            stateStore,
            new FixedTimeProvider(Utc(2026, 7, 13, 5, 5)));

        var result = await controller.GetMarketSession("STO", "G1", "HOSE", CancellationToken.None);

        var session = AssertOkSession(result);
        Assert.Equal(MarketSessionPhases.LunchBreak, session.Phase);
        Assert.Equal(MarketSessionSources.ScheduleFallback, session.Source);
        Assert.False(session.IsContinuous);
        Assert.False(session.IsOpen);
    }

    private static MarketSessionUpdateDto AssertOkSession(ActionResult<MarketSessionUpdateDto> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<MarketSessionUpdateDto>(ok.Value);
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute)
    {
        return new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }

    private sealed class ThrowingMarketDataProvider : IMarketDataProvider
    {
        public Task<IReadOnlyList<MarketQuoteDto>> GetMarketBoardAsync(
            MarketBoardQuery query,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<SymbolDetailDto?> GetSymbolDetailAsync(
            string symbol,
            string boardId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<MarketIndexDto>> GetMarketIndicesAsync(
            IReadOnlyCollection<string> indexNames,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<OhlcBarDto>> GetOhlcAsync(
            string symbol,
            string resolution,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<OhlcBarDto>> GetIndexOhlcAsync(
            string indexName,
            string resolution,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<MarketTradeDto>> GetLatestTradesAsync(
            string symbol,
            string boardId,
            int limit,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
