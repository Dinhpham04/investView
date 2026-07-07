using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace InvestView.Infrastructure.Dnse;

public sealed class DnseRestSigner
{
    public DnseRestSignature Sign(
        string apiKey,
        string apiSecret,
        string method,
        string path,
        string dateHeaderName,
        string dateHeaderValue,
        string? nonce,
        string algorithm)
    {
        var normalizedAlgorithm = string.IsNullOrWhiteSpace(algorithm)
            ? "hmac-sha256"
            : algorithm.Trim().ToLowerInvariant();
        var normalizedDateHeaderName = string.IsNullOrWhiteSpace(dateHeaderName)
            ? DnseMarketDataOptions.DefaultDateHeaderName
            : dateHeaderName.Trim();
        var dateHeaderKey = normalizedDateHeaderName.ToLowerInvariant();

        var headersList = $"(request-target) {dateHeaderKey}";
        var signingString = $"(request-target): {method.ToLowerInvariant()} {path}\n{dateHeaderKey}: {dateHeaderValue}";
        if (!string.IsNullOrWhiteSpace(nonce))
        {
            signingString += $"\nnonce: {nonce}";
        }

        var signature = CreateEncodedSignature(apiSecret, signingString, normalizedAlgorithm);
        var signatureHeaderValue =
            $"Signature keyId=\"{apiKey}\",algorithm=\"{normalizedAlgorithm}\",headers=\"{headersList}\",signature=\"{signature}\"";
        if (!string.IsNullOrWhiteSpace(nonce))
        {
            signatureHeaderValue += $",nonce=\"{nonce}\"";
        }

        return new DnseRestSignature(normalizedDateHeaderName, dateHeaderValue, signatureHeaderValue, nonce);
    }

    public static string FormatDateHeader(DateTimeOffset utcNow)
    {
        return utcNow
            .ToUniversalTime()
            .ToString("ddd, dd MMM yyyy HH:mm:ss", CultureInfo.InvariantCulture) + " +0000";
    }

    private static string CreateEncodedSignature(string apiSecret, string signingString, string algorithm)
    {
        using HMAC hmac = algorithm switch
        {
            "hmac-sha256" => new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret)),
            "hmac-sha384" => new HMACSHA384(Encoding.UTF8.GetBytes(apiSecret)),
            "hmac-sha512" => new HMACSHA512(Encoding.UTF8.GetBytes(apiSecret)),
            _ => new HMACSHA1(Encoding.UTF8.GetBytes(apiSecret))
        };

        var digest = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingString));
        return Uri.EscapeDataString(Convert.ToBase64String(digest));
    }
}
