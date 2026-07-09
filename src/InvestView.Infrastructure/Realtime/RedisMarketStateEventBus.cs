using System.Text.Json;
using InvestView.Infrastructure.MarketData;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace InvestView.Infrastructure.Realtime;

public sealed class RedisMarketStateEventBus : IMarketStateEventBus
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ISubscriber _subscriber;
    private readonly MarketStateOptions _options;

    public RedisMarketStateEventBus(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<MarketStateOptions> options)
    {
        _subscriber = connectionMultiplexer.GetSubscriber();
        _options = options.Value;
    }

    public async Task PublishAsync(MarketStateEvent marketEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = JsonSerializer.Serialize(marketEvent, SerializerOptions);
        await _subscriber.PublishAsync(RedisChannel.Literal(ChannelName()), payload);
    }

    public string ChannelName()
    {
        return string.IsNullOrWhiteSpace(_options.RedisChannelName)
            ? "investview:market-state-events"
            : _options.RedisChannelName.Trim();
    }
}
