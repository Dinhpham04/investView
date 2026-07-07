using InvestView.Infrastructure.Dnse;

namespace InvestView.Api.Tests.Dnse;

public sealed class DnseMarketDataClientTests
{
    [Fact]
    public void BuildRequestUri_AddsQueryStringWithoutChangingPath()
    {
        var uri = DnseMarketDataClient.BuildRequestUri(
            new Uri("https://openapi.dnse.com.vn"),
            "/price/HPG/quotes/latest",
            new Dictionary<string, string?>
            {
                ["boardId"] = "G1",
                ["empty"] = null
            });

        Assert.Equal("https://openapi.dnse.com.vn/price/HPG/quotes/latest?boardId=G1", uri.ToString());
        Assert.Equal("/price/HPG/quotes/latest", uri.AbsolutePath);
    }

    [Fact]
    public void BuildRequestUri_EncodesInstrumentSymbolList()
    {
        var uri = DnseMarketDataClient.BuildRequestUri(
            new Uri("https://openapi.dnse.com.vn/"),
            "/instruments",
            new Dictionary<string, string?>
            {
                ["symbol"] = "SSI,SHS,ACB",
                ["limit"] = "100",
                ["page"] = "1"
            });

        Assert.Equal(
            "https://openapi.dnse.com.vn/instruments?symbol=SSI%2CSHS%2CACB&limit=100&page=1",
            uri.ToString());
    }
}
