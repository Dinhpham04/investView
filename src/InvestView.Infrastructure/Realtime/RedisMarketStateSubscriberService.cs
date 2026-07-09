using System.Text.Json;
using InvestView.Infrastructure.MarketData;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace InvestView.Infrastructure.Realtime;

public sealed class RedisMarketStateSubscriberService : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly IMarketStateEventHandler _handler;
    private readonly MarketStateOptions _options;
    private readonly ILogger<RedisMarketStateSubscriberService> _logger;

    public RedisMarketStateSubscriberService(
        IConnectionMultiplexer connectionMultiplexer,
        IMarketStateEventHandler handler,
        IOptions<MarketStateOptions> options,
        ILogger<RedisMarketStateSubscriberService> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _handler = handler;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = _connectionMultiplexer.GetSubscriber();
        var channel = RedisChannel.Literal(ChannelName());
        var queue = await subscriber.SubscribeAsync(channel, CommandFlags.None);
        queue.OnMessage(message => HandleMessageAsync(message.Message, stoppingToken));
        _logger.LogInformation("Subscribed to Redis market-state channel {Channel}.", ChannelName());

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await queue.UnsubscribeAsync(CommandFlags.None);
        }
    }

    private async Task HandleMessageAsync(RedisValue payload, CancellationToken cancellationToken)
    {
        try
        {
            var marketEvent = JsonSerializer.Deserialize<MarketStateEvent>((string)payload!, SerializerOptions);
            if (marketEvent is null)
            {
                return;
            }

            await _handler.HandleAsync(marketEvent, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to process Redis market-state event.");
        }
    }

    private string ChannelName()
    {
        return string.IsNullOrWhiteSpace(_options.RedisChannelName)
            ? "investview:market-state-events"
            : _options.RedisChannelName.Trim();
    }
}
