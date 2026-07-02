using InvestView.Application;

namespace InvestView.Application.Tests;

public sealed class ApplicationAssemblyTests
{
    [Fact]
    public void ApplicationAssemblyMarker_PointsToApplicationAssembly()
    {
        Assert.Equal("InvestView.Application", ApplicationAssembly.Marker.Assembly.GetName().Name);
    }
}
