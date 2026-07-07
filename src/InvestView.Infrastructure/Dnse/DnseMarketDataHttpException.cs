using System.Net;

namespace InvestView.Infrastructure.Dnse;

public sealed class DnseMarketDataHttpException : HttpRequestException
{
    public DnseMarketDataHttpException(
        HttpStatusCode statusCode,
        string requestUri,
        string responseBody)
        : base($"DNSE request failed with {(int)statusCode} ({statusCode}) for {requestUri}. Body: {responseBody}", null, statusCode)
    {
        RequestUri = requestUri;
        ResponseBody = responseBody;
    }

    public string RequestUri { get; }

    public string ResponseBody { get; }
}
