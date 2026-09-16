using GitPulse.Services;
using Xunit;

namespace GitPulse.Tests;

public class DeadServicesContractTests
{
    [Fact]
    public void ServicesAssembly_DoesNotExposeAssemblyMarker()
    {
        Assert.Null(typeof(NotificationPoller).Assembly.GetType("GitPulse.Services.ServicesAssemblyMarker"));
    }
}
