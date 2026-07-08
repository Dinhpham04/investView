using InvestView.Infrastructure.Dnse;

namespace InvestView.Api.Tests.Dnse;

public sealed class DnseWebSocketAuthSignerTests
{
    [Fact]
    public void ComputeSignature_SignsApiKeyTimestampAndNonceWithHmacSha256Hex()
    {
        var signature = DnseWebSocketAuthSigner.ComputeSignature(
            apiKey: "key",
            apiSecret: "secret",
            timestamp: 1_710_000_000,
            nonce: "nonce-1");

        Assert.Equal("1c828afbb19b0490a2615f637f6c08e103adf1d8c2b82bd5415432f8fefb329b", signature);
    }
}
