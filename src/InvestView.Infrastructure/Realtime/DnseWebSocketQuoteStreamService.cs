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
        MarketDataChannel.TopPrice,
        MarketDataChannel.Foreign
    ];

    private readonly IMarketQuoteBroadcaster _broadcaster;
    private readonly DnseWebSocketAuthSigner _authSigner;
    private readonly DnseWebSocketMessageMapper _messageMapper;
    private readonly DnseQuoteUpdateAggregator _updateAggregator;
    private readonly MarketQuoteStreamOptions _streamOptions;
    private readonly DnseMarketDataOptions _dnseOptions;
    private readonly ILogger<DnseWebSocketQuoteStreamService> _logger;
    private readonly TimeProvider _timeProvider;

    public DnseWebSocketQuoteStreamService(
        IMarketQuoteBroadcaster broadcaster,
        DnseWebSocketAuthSigner authSigner,
        DnseWebSocketMessageMapper messageMapper,
        DnseQuoteUpdateAggregator updateAggregator,
        IOptions<MarketQuoteStreamOptions> streamOptions,
        IOptions<DnseMarketDataOptions> dnseOptions,
        ILogger<DnseWebSocketQuoteStreamService> logger,
        TimeProvider timeProvider)
    {
        _broadcaster = broadcaster;
        _authSigner = authSigner;
        _messageMapper = messageMapper;
        _updateAggregator = updateAggregator;
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

        while (!stoppingToken.IsCancellationRequested)
        {
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
        await SubscribeAsync(webSocket, cancellationToken);
        await BroadcastStatusAsync(true, "DNSE websocket connected and subscribed.", cancellationToken);

        while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var json = await ReceiveTextAsync(webSocket, cancellationToken);
            if (json is null)
            {
                break;
            }

            await HandleMessageAsync(webSocket, json, cancellationToken);
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

    private async Task SubscribeAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        var subscribePayload = DnseWebSocketSubscriptionBuilder.BuildSubscribePayload(
            _streamOptions.Symbols,
            _streamOptions.BoardId,
            _dnseOptions.WebSocketEncoding,
            MarketBoardChannels);

        await SendJsonAsync(webSocket, subscribePayload, cancellationToken);
        _logger.LogInformation(
            "Subscribed DNSE websocket channels {Channels} for symbols {Symbols}.",
            string.Join(", ", subscribePayload.Channels.Select(channel => channel.Name)),
            string.Join(", ", subscribePayload.Channels.FirstOrDefault()?.Symbols ?? Array.Empty<string>()));
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
                await _broadcaster.BroadcastQuoteUpdateAsync(update, cancellationToken);
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
