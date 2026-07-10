using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Abstractions.Realtime;
using InvestView.Application.Dtos.Realtime;
using InvestView.Infrastructure.Dnse;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestView.Infrastructure.Realtime;

public sealed class DnseWebSocketQuoteStreamService : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyCollection<MarketDataChannel> MarketBoardChannels =
    [
        MarketDataChannel.SecurityDefinition,
        MarketDataChannel.Trade,
        MarketDataChannel.TradeExtra,
        MarketDataChannel.TopPrice,
        MarketDataChannel.Foreign,
        MarketDataChannel.ExpectedPrice
    ];

    private readonly IMarketQuoteBroadcaster _broadcaster;
    private readonly IMarketStateEventPublisher _marketStateEventPublisher;
    private readonly DnseWebSocketAuthSigner _authSigner;
    private readonly DnseWebSocketMessageMapper _messageMapper;
    private readonly DnseQuoteUpdateAggregator _updateAggregator;
    private readonly IMarketQuoteSubscriptionRegistry _subscriptionRegistry;
    private readonly MarketQuoteStreamSchedule _streamSchedule;
    private readonly MarketQuoteStreamOptions _streamOptions;
    private readonly DnseMarketDataOptions _dnseOptions;
    private readonly ILogger<DnseWebSocketQuoteStreamService> _logger;
    private readonly TimeProvider _timeProvider;

    public DnseWebSocketQuoteStreamService(
        IMarketQuoteBroadcaster broadcaster,
        IMarketStateEventPublisher marketStateEventPublisher,
        DnseWebSocketAuthSigner authSigner,
        DnseWebSocketMessageMapper messageMapper,
        DnseQuoteUpdateAggregator updateAggregator,
        IMarketQuoteSubscriptionRegistry subscriptionRegistry,
        MarketQuoteStreamSchedule streamSchedule,
        IOptions<MarketQuoteStreamOptions> streamOptions,
        IOptions<DnseMarketDataOptions> dnseOptions,
        ILogger<DnseWebSocketQuoteStreamService> logger,
        TimeProvider timeProvider)
    {
        _broadcaster = broadcaster;
        _marketStateEventPublisher = marketStateEventPublisher;
        _authSigner = authSigner;
        _messageMapper = messageMapper;
        _updateAggregator = updateAggregator;
        _subscriptionRegistry = subscriptionRegistry;
        _streamSchedule = streamSchedule;
        _streamOptions = streamOptions.Value;
        _dnseOptions = dnseOptions.Value;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_streamOptions.Enabled || !_streamOptions.UsesDnseWebSocketSourceProvider())
        {
            _logger.LogInformation("DNSE websocket quote stream is disabled or not selected.");
            return;
        }

        if (!_dnseOptions.HasCredentials)
        {
            _logger.LogWarning("DNSE websocket quote stream is selected but credentials are missing.");
            await BroadcastStatusAsync(false, "DNSE websocket stream is missing API credentials.", stoppingToken);
            return;
        }

        var reconnectDelay = TimeSpan.FromSeconds(Math.Max(1, _dnseOptions.WebSocketReconnectInitialDelaySeconds));
        var maxReconnectDelay = TimeSpan.FromSeconds(Math.Max(1, _dnseOptions.WebSocketReconnectMaxDelaySeconds));
        string? lastGateMessage = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            var subscriptionSnapshot = _subscriptionRegistry.GetSnapshot();
            var gateDecision = _streamSchedule.Evaluate(subscriptionSnapshot, _timeProvider.GetUtcNow());
            if (!gateDecision.ShouldConnect)
            {
                if (!string.Equals(lastGateMessage, gateDecision.Message, StringComparison.Ordinal))
                {
                    _logger.LogInformation("{Message} Rechecking in {Delay}.", gateDecision.Message, gateDecision.RecheckAfter);
                    await BroadcastStatusAsync(false, gateDecision.Message, stoppingToken);
                    lastGateMessage = gateDecision.Message;
                }

                await WaitForConnectionGateChangeAsync(subscriptionSnapshot, gateDecision, stoppingToken);
                continue;
            }

            lastGateMessage = null;

            try
            {
                await RunConnectionAsync(stoppingToken);
                reconnectDelay = TimeSpan.FromSeconds(Math.Max(1, _dnseOptions.WebSocketReconnectInitialDelaySeconds));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "DNSE websocket quote stream failed. Reconnecting in {Delay}.", reconnectDelay);
                await BroadcastStatusAsync(true, $"DNSE websocket reconnecting in {reconnectDelay.TotalSeconds:0}s.", stoppingToken);
                await Task.Delay(reconnectDelay, stoppingToken);
                reconnectDelay = TimeSpan.FromSeconds(Math.Min(reconnectDelay.TotalSeconds * 2, maxReconnectDelay.TotalSeconds));
            }
        }
    }

    private async Task WaitForConnectionGateChangeAsync(
        MarketQuoteSubscriptionSnapshot snapshot,
        MarketQuoteStreamConnectionDecision decision,
        CancellationToken cancellationToken)
    {
        var delayTask = Task.Delay(decision.RecheckAfter, cancellationToken);
        var subscriptionTask = _subscriptionRegistry
            .WaitForChangeAsync(snapshot.Version, cancellationToken)
            .AsTask();

        var completedTask = await Task.WhenAny(delayTask, subscriptionTask);
        if (completedTask == subscriptionTask)
        {
            await subscriptionTask;
        }
    }

    private async Task RunConnectionAsync(CancellationToken cancellationToken)
    {
        using var webSocket = new ClientWebSocket();
        webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(Math.Max(30, _dnseOptions.WebSocketKeepAliveSeconds));

        var uri = BuildStreamUri();
        _logger.LogInformation("Connecting DNSE websocket quote stream to {Uri}.", uri);
        await BroadcastStatusAsync(true, "DNSE websocket connecting.", cancellationToken);

        using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectTimeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _dnseOptions.WebSocketConnectionTimeoutSeconds)));
        await webSocket.ConnectAsync(uri, connectTimeout.Token);

        await AuthenticateAsync(webSocket, cancellationToken);
        var activeSubscriptions = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var activeSessionSubscriptions = new HashSet<string>(StringComparer.Ordinal);
        var marketIndicesSubscribed = false;
        var estimatedMarketIndicesSubscribed = false;
        var subscriptionSnapshot = _subscriptionRegistry.GetSnapshot();
        (marketIndicesSubscribed, estimatedMarketIndicesSubscribed) = await ApplySubscriptionsAsync(
            webSocket,
            subscriptionSnapshot,
            activeSubscriptions,
            activeSessionSubscriptions,
            marketIndicesSubscribed,
            estimatedMarketIndicesSubscribed,
            cancellationToken);
        await BroadcastStatusAsync(true, "DNSE websocket connected.", cancellationToken);

        var receiveTask = ReceiveTextAsync(webSocket, cancellationToken);
        var subscriptionTask = _subscriptionRegistry
            .WaitForChangeAsync(subscriptionSnapshot.Version, cancellationToken)
            .AsTask();
        var scheduleTask = Task.Delay(
            _streamSchedule.Evaluate(subscriptionSnapshot, _timeProvider.GetUtcNow()).RecheckAfter,
            cancellationToken);

        while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var completedTask = await Task.WhenAny(receiveTask, subscriptionTask, scheduleTask);

            if (completedTask == receiveTask)
            {
                var json = await receiveTask;
                if (json is null)
                {
                    break;
                }

                await HandleMessageAsync(webSocket, json, cancellationToken);
                receiveTask = ReceiveTextAsync(webSocket, cancellationToken);
                continue;
            }

            if (completedTask == scheduleTask)
            {
                var gateDecision = _streamSchedule.Evaluate(subscriptionSnapshot, _timeProvider.GetUtcNow());
                if (!gateDecision.ShouldConnect)
                {
                    _logger.LogInformation("{Message} Closing DNSE websocket connection.", gateDecision.Message);
                    await BroadcastStatusAsync(false, gateDecision.Message, cancellationToken);
                    break;
                }

                scheduleTask = Task.Delay(gateDecision.RecheckAfter, cancellationToken);
                continue;
            }

            subscriptionSnapshot = await subscriptionTask;
            var subscriptionGateDecision = _streamSchedule.Evaluate(subscriptionSnapshot, _timeProvider.GetUtcNow());
            if (!subscriptionGateDecision.ShouldConnect)
            {
                _logger.LogInformation("{Message} Closing DNSE websocket connection.", subscriptionGateDecision.Message);
                await BroadcastStatusAsync(false, subscriptionGateDecision.Message, cancellationToken);
                break;
            }

            (marketIndicesSubscribed, estimatedMarketIndicesSubscribed) = await ApplySubscriptionsAsync(
                webSocket,
                subscriptionSnapshot,
                activeSubscriptions,
                activeSessionSubscriptions,
                marketIndicesSubscribed,
                estimatedMarketIndicesSubscribed,
                cancellationToken);
            subscriptionTask = _subscriptionRegistry
                .WaitForChangeAsync(subscriptionSnapshot.Version, cancellationToken)
                .AsTask();
            scheduleTask = Task.Delay(subscriptionGateDecision.RecheckAfter, cancellationToken);
        }
    }

    private async Task AuthenticateAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        var welcome = await ReceiveTextAsync(webSocket, cancellationToken);
        _logger.LogDebug("DNSE websocket welcome message: {Welcome}", welcome);

        var timestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var nonce = Guid.NewGuid().ToString("N");
        var signature = _authSigner.CreateSignature(_dnseOptions.ApiKey, _dnseOptions.ApiSecret, timestamp, nonce);
        var authMessage = new Dictionary<string, object>
        {
            ["action"] = "auth",
            ["api_key"] = _dnseOptions.ApiKey,
            ["signature"] = signature,
            ["timestamp"] = timestamp,
            ["nonce"] = nonce
        };

        await SendJsonAsync(webSocket, authMessage, cancellationToken);

        var authResponse = await ReceiveTextAsync(webSocket, cancellationToken)
            ?? throw new WebSocketException("DNSE websocket closed before auth response.");
        var mappedResponse = _messageMapper.Map(authResponse);
        if (mappedResponse.Kind != DnseWebSocketMessageKind.AuthSuccess)
        {
            throw new InvalidOperationException(mappedResponse.ErrorMessage ?? "DNSE websocket authentication failed.");
        }
    }

    private async Task<(bool MarketIndicesSubscribed, bool EstimatedMarketIndicesSubscribed)> ApplySubscriptionsAsync(
        ClientWebSocket webSocket,
        MarketQuoteSubscriptionSnapshot snapshot,
        Dictionary<string, HashSet<string>> activeSubscriptions,
        HashSet<string> activeSessionSubscriptions,
        bool marketIndicesSubscribed,
        bool estimatedMarketIndicesSubscribed,
        CancellationToken cancellationToken)
    {
        if (snapshot.Boards.Count == 0)
        {
            _logger.LogInformation("DNSE websocket is connected and waiting for active market-board subscriptions.");
            await BroadcastStatusAsync(true, "DNSE websocket connected; waiting for market-board subscriptions.", cancellationToken);
            return (marketIndicesSubscribed, estimatedMarketIndicesSubscribed);
        }

        foreach (var boardSubscription in snapshot.Boards)
        {
            if (!activeSubscriptions.TryGetValue(boardSubscription.BoardId, out var activeSymbols))
            {
                activeSymbols = new HashSet<string>(StringComparer.Ordinal);
                activeSubscriptions[boardSubscription.BoardId] = activeSymbols;
            }

            var newSymbols = boardSubscription
                .Symbols
                .Where(symbol => !activeSymbols.Contains(symbol))
                .ToArray();

            if (newSymbols.Length == 0)
            {
                continue;
            }

            var subscribePayload = DnseWebSocketSubscriptionBuilder.BuildSubscribePayload(
                newSymbols,
                boardSubscription.BoardId,
                _dnseOptions.WebSocketEncoding,
                MarketBoardChannels);

            await SendJsonAsync(webSocket, subscribePayload, cancellationToken);
            await SendOhlcSubscriptionsAsync(webSocket, newSymbols, cancellationToken);

            foreach (var symbol in newSymbols)
            {
                activeSymbols.Add(symbol);
            }

            _logger.LogInformation(
                "Subscribed DNSE websocket channels {Channels} for board {BoardId} symbols {Symbols}.",
                string.Join(", ", subscribePayload.Channels.Select(channel => channel.Name)),
                boardSubscription.BoardId,
                string.Join(", ", newSymbols));
            await BroadcastStatusAsync(
                true,
                $"DNSE websocket subscribed {newSymbols.Length} new symbol(s) for {boardSubscription.BoardId}.",
                cancellationToken);

            if (activeSessionSubscriptions.Add(boardSubscription.BoardId))
            {
                var sessionPayload = DnseWebSocketSubscriptionBuilder.BuildSessionSubscribePayload(
                    [boardSubscription.BoardId],
                    _dnseOptions.WebSocketProductGroupId,
                    _dnseOptions.WebSocketEncoding);
                await SendJsonAsync(webSocket, sessionPayload, cancellationToken);
                _logger.LogInformation(
                    "Subscribed DNSE websocket session channels {Channels}.",
                    string.Join(", ", sessionPayload.Channels.Select(channel => channel.Name)));
            }
        }

        if (!marketIndicesSubscribed)
        {
            var indexNames = _dnseOptions.DefaultMarketIndices
                .Select(indexName => indexName.Trim().ToUpperInvariant())
                .Where(indexName => !string.IsNullOrWhiteSpace(indexName))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (indexNames.Length > 0)
            {
                var subscribePayload = DnseWebSocketSubscriptionBuilder.BuildMarketIndexSubscribePayload(
                    indexNames,
                    _dnseOptions.WebSocketEncoding);

                await SendJsonAsync(webSocket, subscribePayload, cancellationToken);
                await SendOhlcSubscriptionsAsync(webSocket, indexNames, cancellationToken);
                marketIndicesSubscribed = true;

                _logger.LogInformation(
                    "Subscribed DNSE websocket market index channels {Channels}.",
                    string.Join(", ", subscribePayload.Channels.Select(channel => channel.Name)));
            }
        }

        if (!estimatedMarketIndicesSubscribed)
        {
            var indexNames = _dnseOptions.DefaultMarketIndices
                .Select(indexName => indexName.Trim().ToUpperInvariant())
                .Where(indexName => !string.IsNullOrWhiteSpace(indexName))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (indexNames.Length > 0)
            {
                var subscribePayload = DnseWebSocketSubscriptionBuilder.BuildEstimatedMarketIndexSubscribePayload(
                    indexNames,
                    _dnseOptions.WebSocketEncoding);

                await SendJsonAsync(webSocket, subscribePayload, cancellationToken);
                estimatedMarketIndicesSubscribed = true;

                _logger.LogInformation(
                    "Subscribed DNSE websocket estimated market index channels {Channels}.",
                    string.Join(", ", subscribePayload.Channels.Select(channel => channel.Name)));
            }
        }

        return (marketIndicesSubscribed, estimatedMarketIndicesSubscribed);
    }

    private async Task SendOhlcSubscriptionsAsync(
        ClientWebSocket webSocket,
        IReadOnlyCollection<string> instruments,
        CancellationToken cancellationToken)
    {
        var resolutions = _dnseOptions.WebSocketOhlcResolutions
            .Select(resolution => resolution.Trim().ToUpperInvariant())
            .Where(resolution => !string.IsNullOrWhiteSpace(resolution))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (resolutions.Length == 0)
        {
            return;
        }

        var openPayload = DnseWebSocketSubscriptionBuilder.BuildOhlcSubscribePayload(
            instruments,
            resolutions,
            _dnseOptions.WebSocketEncoding,
            closed: false);
        var closedPayload = DnseWebSocketSubscriptionBuilder.BuildOhlcSubscribePayload(
            instruments,
            resolutions,
            _dnseOptions.WebSocketEncoding,
            closed: true);

        await SendJsonAsync(webSocket, openPayload, cancellationToken);
        await SendJsonAsync(webSocket, closedPayload, cancellationToken);

        _logger.LogInformation(
            "Subscribed DNSE websocket OHLC channels {OpenChannels} and closed channels {ClosedChannels} for instruments {Instruments}.",
            string.Join(", ", openPayload.Channels.Select(channel => channel.Name)),
            string.Join(", ", closedPayload.Channels.Select(channel => channel.Name)),
            string.Join(", ", instruments));
    }

    private async Task HandleMessageAsync(ClientWebSocket webSocket, string json, CancellationToken cancellationToken)
    {
        var message = _messageMapper.Map(json);
        switch (message.Kind)
        {
            case DnseWebSocketMessageKind.Ping:
                await SendJsonAsync(webSocket, new Dictionary<string, string> { ["action"] = "pong" }, cancellationToken);
                break;
            case DnseWebSocketMessageKind.QuoteUpdate when message.QuoteUpdate is not null:
                var update = _updateAggregator.Apply(message.QuoteUpdate);
                await _marketStateEventPublisher.PublishQuoteUpdateAsync(update, cancellationToken);
                break;
            case DnseWebSocketMessageKind.TradeUpdate when message.TradeUpdate is not null:
                await _marketStateEventPublisher.PublishTradeUpdateAsync(message.TradeUpdate, cancellationToken);
                break;
            case DnseWebSocketMessageKind.MarketIndexUpdate when message.MarketIndexUpdate is not null:
                await _marketStateEventPublisher.PublishMarketIndexUpdateAsync(message.MarketIndexUpdate, cancellationToken);
                break;
            case DnseWebSocketMessageKind.OhlcUpdate when message.OhlcUpdate is not null:
                await _marketStateEventPublisher.PublishOhlcUpdateAsync(message.OhlcUpdate, cancellationToken);
                break;
            case DnseWebSocketMessageKind.MarketSessionUpdate when message.MarketSessionUpdate is not null:
                await _marketStateEventPublisher.PublishMarketSessionUpdateAsync(message.MarketSessionUpdate, cancellationToken);
                break;
            case DnseWebSocketMessageKind.Error:
                _logger.LogWarning("DNSE websocket error message: {Message}", message.ErrorMessage);
                await BroadcastStatusAsync(true, message.ErrorMessage ?? "DNSE websocket error.", cancellationToken);
                break;
            case DnseWebSocketMessageKind.Subscribed:
            case DnseWebSocketMessageKind.Pong:
            case DnseWebSocketMessageKind.AuthSuccess:
            case DnseWebSocketMessageKind.Unknown:
                _logger.LogDebug("Ignored DNSE websocket message kind {Kind}.", message.Kind);
                break;
        }
    }

    private async Task SendJsonAsync(ClientWebSocket webSocket, object message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        await webSocket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
    }

    private async Task<string?> ReceiveTextAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();
        using var receiveTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        receiveTimeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _dnseOptions.WebSocketReceiveTimeoutSeconds)));

        WebSocketReceiveResult result;
        do
        {
            result = await webSocket.ReceiveAsync(buffer, receiveTimeout.Token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            stream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private Uri BuildStreamUri()
    {
        var baseUrl = string.IsNullOrWhiteSpace(_dnseOptions.WebSocketBaseUrl)
            ? DnseMarketDataOptions.DefaultWebSocketBaseUrl
            : _dnseOptions.WebSocketBaseUrl.TrimEnd('/');
        var url = baseUrl.EndsWith("/v1/stream", StringComparison.OrdinalIgnoreCase)
            ? baseUrl
            : $"{baseUrl}/v1/stream";
        var builder = new UriBuilder(url)
        {
            Query = $"encoding={Uri.EscapeDataString(_dnseOptions.WebSocketEncoding)}"
        };

        return builder.Uri;
    }

    private Task BroadcastStatusAsync(bool isEnabled, string message, CancellationToken cancellationToken)
    {
        return _broadcaster.BroadcastStreamStatusAsync(
            new QuoteStreamStatusDto("DnseWebSocket", isEnabled, _timeProvider.GetUtcNow(), message),
            cancellationToken);
    }
}
