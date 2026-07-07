using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InvestView.Infrastructure.Dnse;

public sealed class DnseMarketDataClient : IDnseMarketDataClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DnseMarketDataClient> _logger;
    private readonly DnseMarketDataOptions _options;
    private readonly DnseRestSigner _signer;

    public DnseMarketDataClient(
        HttpClient httpClient,
        IOptions<DnseMarketDataOptions> options,
        DnseRestSigner signer,
        ILogger<DnseMarketDataClient>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger ?? NullLogger<DnseMarketDataClient>.Instance;
        _options = options.Value;
        _signer = signer;
    }

    public async Task<JsonDocument> GetJsonAsync(
        string path,
        IReadOnlyDictionary<string, string?>? query,
        CancellationToken cancellationToken)
    {
        var baseUri = new Uri(_options.BaseUrl.TrimEnd('/'));
        var uri = BuildRequestUri(baseUri, path, query);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);

        var dateValue = DnseRestSigner.FormatDateHeader(DateTimeOffset.UtcNow);
        var nonce = _options.HmacNonceEnabled ? Guid.NewGuid().ToString("N") : null;
        var signature = _signer.Sign(
            _options.ApiKey,
            _options.ApiSecret,
            request.Method.Method,
            uri.AbsolutePath,
            _options.DateHeaderName,
            dateValue,
            nonce,
            _options.Algorithm);

        request.Headers.TryAddWithoutValidation(signature.DateHeaderName, signature.DateHeaderValue);
        request.Headers.TryAddWithoutValidation("X-Signature", signature.SignatureHeaderValue);
        request.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);
        request.Headers.TryAddWithoutValidation("version", _options.ApiVersion);

        _logger.LogInformation("DNSE request: GET {PathAndQuery}", uri.PathAndQuery);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        LogResponseBody(uri.PathAndQuery, (int)response.StatusCode, responseBody);

        if (!response.IsSuccessStatusCode)
        {
            throw new DnseMarketDataHttpException(
                response.StatusCode,
                uri.PathAndQuery,
                responseBody);
        }

        return JsonDocument.Parse(responseBody);
    }

    private void LogResponseBody(string pathAndQuery, int statusCode, string responseBody)
    {
        if (!_options.LogResponseBodies)
        {
            _logger.LogInformation(
                "DNSE response: {StatusCode} {PathAndQuery} ({BodyLength} chars)",
                statusCode,
                pathAndQuery,
                responseBody.Length);
            return;
        }

        var maxLength = Math.Max(_options.MaxLoggedResponseBodyChars, 256);
        var truncatedBody = responseBody.Length <= maxLength
            ? responseBody
            : responseBody[..maxLength] + "... [truncated]";

        _logger.LogInformation(
            "DNSE response body: {StatusCode} {PathAndQuery} ({BodyLength} chars) {ResponseBody}",
            statusCode,
            pathAndQuery,
            responseBody.Length,
            truncatedBody);
    }

    public static Uri BuildRequestUri(
        Uri baseUri,
        string path,
        IReadOnlyDictionary<string, string?>? query)
    {
        var normalizedPath = path.StartsWith('/') ? path : "/" + path;
        var builder = new UriBuilder(new Uri(baseUri, normalizedPath));

        var queryParts = query?
            .Where(item => item.Value is not null)
            .Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value!)}")
            .ToArray();

        if (queryParts is { Length: > 0 })
        {
            builder.Query = string.Join('&', queryParts);
        }

        return builder.Uri;
    }
}
