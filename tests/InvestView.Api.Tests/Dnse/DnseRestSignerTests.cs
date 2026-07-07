using InvestView.Infrastructure.Dnse;

namespace InvestView.Api.Tests.Dnse;

public sealed class DnseRestSignerTests
{
    [Fact]
    public void Sign_BuildsSdkCompatibleSignatureHeader()
    {
        var signer = new DnseRestSigner();

        var signature = signer.Sign(
            apiKey: "test-key",
            apiSecret: "test-secret",
            method: "GET",
            path: "/price/HPG/quotes/latest",
            dateHeaderName: "Date",
            dateHeaderValue: "Fri, 15 May 2026 07:11:30 +0000",
            nonce: "26c4b530cf12427d95bf691e39aa8d74",
            algorithm: "hmac-sha256");

        Assert.Equal("Date", signature.DateHeaderName);
        Assert.Equal("Fri, 15 May 2026 07:11:30 +0000", signature.DateHeaderValue);
        Assert.Equal("26c4b530cf12427d95bf691e39aa8d74", signature.Nonce);
        Assert.Equal(
            "Signature keyId=\"test-key\",algorithm=\"hmac-sha256\",headers=\"(request-target) date\",signature=\"osZF%2BYCUrlKsr6M2n%2FzCtil2%2BKvB9Gqti64JX%2FRx%2FpU%3D\",nonce=\"26c4b530cf12427d95bf691e39aa8d74\"",
            signature.SignatureHeaderValue);
    }

    [Fact]
    public void Sign_UsesConfiguredDateHeaderNameInSigningString()
    {
        var signer = new DnseRestSigner();

        var signature = signer.Sign(
            apiKey: "test-key",
            apiSecret: "test-secret",
            method: "GET",
            path: "/instruments",
            dateHeaderName: "X-Aux-Date",
            dateHeaderValue: "Fri, 15 May 2026 07:11:30 +0000",
            nonce: null,
            algorithm: "hmac-sha256");

        Assert.Equal("X-Aux-Date", signature.DateHeaderName);
        Assert.Contains("headers=\"(request-target) x-aux-date\"", signature.SignatureHeaderValue);
        Assert.DoesNotContain("nonce=", signature.SignatureHeaderValue);
    }
}
