using InvestView.Application.Abstractions.Realtime;
using InvestView.Application.Dtos.Realtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestView.Infrastructure.Realtime;

public sealed class MockQuoteStreamService : BackgroundService
{
    private readonly MockQuoteStreamPublisher _publisher;
    private readonly IMarketQuoteBroadcaster _broadcaster;
    private readonly MarketQuoteStreamOptions _options;
    private readonly ILogger<MockQuoteStreamService> _logger;
    private readonly TimeProvider _timeProvider;

    public MockQuoteStreamService(
        MockQuoteStreamPublisher publisher,
        IMarketQuoteBroadcaster broadcaster,
        IOptions<MarketQuoteStreamOptions> options,
        ILogger<MockQuoteStreamService> logger,
        TimeProvider timeProvider)
    {
        _publisher = publisher;
        _broadcaster = broadcaster;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Mock quote stream is disabled.");
            return;
        }

        if (!_options.UsesMockCompatibleSourceProvider())
        {
            _logger.LogInformation(
                "Mock quote stream is skipped because quote stream source provider is {SourceProvider}.",
                _options.SourceProvider);
            return;
        }

        await _broadcaster.BroadcastStreamStatusAsync(
            new QuoteStreamStatusDto("Mock", IsEnabled: true, _timeProvider.GetUtcNow(), "Mock quote stream started."),
            stoppingToken);

        var interval = TimeSpan.FromMilliseconds(Math.Max(_options.IntervalMilliseconds, 250));
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _publisher.PublishOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Mock quote stream publish failed.");
                await _broadcaster.BroadcastStreamStatusAsync(
                    new QuoteStreamStatusDto("Mock", IsEnabled: true, _timeProvider.GetUtcNow(), "Mock quote stream publish failed."),
                    stoppingToken);
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }
}
