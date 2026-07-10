using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using InvestView.Application.Abstractions.MarketData;
using InvestView.Application.Abstractions.Realtime;
using InvestView.Infrastructure.Dnse;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestView.Infrastructure.Realtime;

public sealed class SecurityDefinitionWarmupService : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyCollection<MarketDataChannel> SecurityDefinitionChannels =
    [
        MarketDataChannel.SecurityDefinition
    ];

    private readonly SecurityDefinitionWarmupSymbolResolver _symbolResolver;
    private readonly SecurityDefinitionWarmupSchedule _schedule;
    private readonly IMarketStateStore _marketStateStore;
    private readonly IMarketStateEventPublisher _marketStateEventPublisher;
    private readonly DnseWebSocketAuthSigner _authSigner;
    private readonly DnseWebSocketMessageMapper _messageMapper;
    private readonly DnseQuoteUpdateAggregator _updateAggregator;
    private readonly SecurityDefinitionWarmupOptions _warmupOptions;
    private readonly DnseMarketDataOptions _dnseOptions;
    private readonly ILogger<SecurityDefinitionWarmupService> _logger;
    private readonly TimeProvider _timeProvider;

    public SecurityDefinitionWarmupService(
        SecurityDefinitionWarmupSymbolResolver symbolResolver,
        SecurityDefinitionWarmupSchedule schedule,
        IMarketStateStore marketStateStore,
        IMarketStateEventPublisher marketStateEventPublisher,
        DnseWebSocketAuthSigner authSigner,
        DnseWebSocketMessageMapper messageMapper,
        DnseQuoteUpdateAggregator updateAggregator,
        IOptions<SecurityDefinitionWarmupOptions> warmupOptions,
        IOptions<DnseMarketDataOptions> dnseOptions,
        ILogger<SecurityDefinitionWarmupService> logger,
        TimeProvider timeProvider)
    {
        _symbolResolver = symbolResolver;
        _schedule = schedule;
        _marketStateStore = marketStateStore;
        _marketStateEventPublisher = marketStateEventPublisher;
        _authSigner = authSigner;
        _messageMapper = messageMapper;
        _updateAggregator = updateAggregator;
        _warmupOptions = warmupOptions.Value;
        _dnseOptions = dnseOptions.Value;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_warmupOptions.Enabled)
        {
            _logger.LogInformation("Security definition warmup is disabled.");
            return;
        }

        if (!_dnseOptions.HasCredentials)
        {
            _logger.LogWarning("Security definition warmup is enabled but DNSE credentials are missing.");
            return;
        }

        DateOnly? lastRunLocalDate = null;
        string? lastGateMessage = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            var decision = _schedule.Evaluate(_timeProvider.GetUtcNow(), lastRunLocalDate);
            if (!decision.ShouldRun)
            {
                if (!string.Equals(lastGateMessage, decision.Message, StringComparison.Ordinal))
                {
                    _logger.LogInformation("{Message} Rechecking in {Delay}.", decision.Message, decision.RecheckAfter);
                    lastGateMessage = decision.Message;
                }

                await Task.Delay(decision.RecheckAfter, stoppingToken);
                continue;
            }

            lastGateMessage = null;

            try
            {
                await RunWarmupAsync(stoppingToken);
                lastRunLocalDate = decision.LocalDate;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                var retryDelay = TimeSpan.FromSeconds(Math.Max(1, _warmupOptions.RetryDelaySeconds));
                _logger.LogWarning(exception, "Security definition warmup failed. Retrying in {Delay}.", retryDelay);
                await Task.Delay(retryDelay, stoppingToken);
            }
        }
    }

    private async Task RunWarmupAsync(CancellationToken cancellationToken)
    {
        var resolution = await _symbolResolver.ResolveAsync(cancellationToken);
        await StoreMarketMembershipsAsync(resolution, cancellationToken);

        if (resolution.Symbols.Count == 0)
        {
            _logger.LogWarning("Security definition warmup found no symbols for configured markets {Markets}.", string.Join(", ", _warmupOptions.MarketIds));
            return;
        }

        using var webSocket = new ClientWebSocket();
        webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(Math.Max(30, _dnseOptions.WebSocketKeepAliveSeconds));

        var uri = BuildStreamUri();
        _logger.LogInformation(
            "Connecting DNSE security definition warmup to {Uri} for {SymbolCount} symbols.",
            uri,
            resolution.Symbols.Count);

        using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectTimeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _dnseOptions.WebSocketConnectionTimeoutSeconds)));
        await webSocket.ConnectAsync(uri, connectTimeout.Token);

        await AuthenticateAsync(webSocket, cancellationToken);
        await SubscribeAsync(webSocket, resolution.Symbols, cancellationToken);
        await ReceiveWarmupMessagesAsync(webSocket, resolution.Symbols, cancellationToken);
        await CloseAsync(webSocket, cancellationToken);
    }

    private async Task StoreMarketMembershipsAsync(
        SecurityDefinitionWarmupSymbolResolution resolution,
        CancellationToken cancellationToken)
    {
        foreach (var (marketId, symbols) in resolution.SymbolsByMarket)
        {
            if (symbols.Count == 0)
            {
                _logger.LogWarning("Security definition warmup skipped empty membership update for market {MarketId}.", marketId);
                continue;
            }

            await _marketStateStore.UpsertSymbolMembershipsAsync(
                new MarketBoardQuery([], NormalizeBoardId(_warmupOptions.BoardId), MarketId: marketId),
                symbols,
                cancellationToken);
        }
    }

    private async Task AuthenticateAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        var welcome = await ReceiveTextWithTimeoutAsync(webSocket, cancellationToken);
        _logger.LogDebug("DNSE security definition warmup welcome message: {Welcome}", welcome);

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

        var authResponse = await ReceiveTextWithTimeoutAsync(webSocket, cancellationToken)
            ?? throw new WebSocketException("DNSE security definition warmup closed before auth response.");
        var mappedResponse = _messageMapper.Map(authResponse);
        if (mappedResponse.Kind != DnseWebSocketMessageKind.AuthSuccess)
        {
            throw new InvalidOperationException(mappedResponse.ErrorMessage ?? "DNSE security definition warmup authentication failed.");
        }
    }

    private async Task SubscribeAsync(
        ClientWebSocket webSocket,
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken)
    {
        var batchSize = Math.Max(1, _warmupOptions.SymbolBatchSize);
        foreach (var batch in symbols.Chunk(batchSize))
        {
            var subscribePayload = DnseWebSocketSubscriptionBuilder.BuildSubscribePayload(
                batch,
                NormalizeBoardId(_warmupOptions.BoardId),
                _dnseOptions.WebSocketEncoding,
                SecurityDefinitionChannels);

            await SendJsonAsync(webSocket, subscribePayload, cancellationToken);
            _logger.LogInformation(
                "Subscribed DNSE security definition warmup channel {Channels} for {SymbolCount} symbols.",
                string.Join(", ", subscribePayload.Channels.Select(channel => channel.Name)),
                batch.Length);
        }
    }

    private async Task ReceiveWarmupMessagesAsync(
        ClientWebSocket webSocket,
        IReadOnlyCollection<string> symbols,
        CancellationToken cancellationToken)
    {
        var pendingSymbols = new HashSet<string>(symbols, StringComparer.Ordinal);
        using var runTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runTimeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _warmupOptions.RunTimeoutSeconds)));

        while (webSocket.State == WebSocketState.Open &&
               pendingSymbols.Count > 0 &&
               !runTimeout.IsCancellationRequested)
        {
            string? json;
            try
            {
                json = await ReceiveTextAsync(webSocket, runTimeout.Token);
            }
            catch (OperationCanceledException) when (runTimeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (json is null)
            {
                break;
            }

            await HandleMessageAsync(webSocket, json, pendingSymbols, cancellationToken);
        }

        _logger.LogInformation(
            "Security definition warmup completed with {ReceivedCount}/{TotalCount} symbols.",
            symbols.Count - pendingSymbols.Count,
            symbols.Count);
    }

    private async Task HandleMessageAsync(
        ClientWebSocket webSocket,
        string json,
        HashSet<string> pendingSymbols,
        CancellationToken cancellationToken)
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
                pendingSymbols.Remove(update.Symbol);
                break;
            case DnseWebSocketMessageKind.Error:
                _logger.LogWarning("DNSE security definition warmup error message: {Message}", message.ErrorMessage);
                break;
            case DnseWebSocketMessageKind.Subscribed:
            case DnseWebSocketMessageKind.Pong:
            case DnseWebSocketMessageKind.AuthSuccess:
            case DnseWebSocketMessageKind.Unknown:
            case DnseWebSocketMessageKind.TradeUpdate:
            case DnseWebSocketMessageKind.MarketIndexUpdate:
            case DnseWebSocketMessageKind.OhlcUpdate:
            case DnseWebSocketMessageKind.MarketSessionUpdate:
                _logger.LogDebug("Ignored DNSE security definition warmup message kind {Kind}.", message.Kind);
                break;
        }
    }

    private async Task SendJsonAsync(ClientWebSocket webSocket, object message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        await webSocket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
    }

    private static async Task<string?> ReceiveTextAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();

        WebSocketReceiveResult result;
        do
        {
            result = await webSocket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            stream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private async Task<string?> ReceiveTextWithTimeoutAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        using var receiveTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        receiveTimeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _dnseOptions.WebSocketReceiveTimeoutSeconds)));
        return await ReceiveTextAsync(webSocket, receiveTimeout.Token);
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

    private static async Task CloseAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        if (webSocket.State is not WebSocketState.Open and not WebSocketState.CloseReceived)
        {
            return;
        }

        try
        {
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Security definition warmup complete.", cancellationToken);
        }
        catch (WebSocketException)
        {
        }
    }

    private static string NormalizeBoardId(string boardId)
    {
        return string.IsNullOrWhiteSpace(boardId)
            ? "G1"
            : boardId.Trim().ToUpperInvariant();
    }
}
