using InvestView.Domain;

namespace InvestView.Domain.Tests;

public sealed class DomainAssemblyTests
{
    [Fact]
    public void DomainAssemblyMarker_PointsToDomainAssembly()
    {
        Assert.Equal("InvestView.Domain", DomainAssembly.Marker.Assembly.GetName().Name);
    }
}
