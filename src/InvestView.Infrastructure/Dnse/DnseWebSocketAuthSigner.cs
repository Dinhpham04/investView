using System.Security.Cryptography;
using System.Text;

namespace InvestView.Infrastructure.Dnse;

public sealed class DnseWebSocketAuthSigner
{
    public string CreateSignature(string apiKey, string apiSecret, long timestamp, string nonce)
    {
        return ComputeSignature(apiKey, apiSecret, timestamp, nonce);
    }

    public static string ComputeSignature(string apiKey, string apiSecret, long timestamp, string nonce)
    {
        var message = $"{apiKey}:{timestamp}:{nonce}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
