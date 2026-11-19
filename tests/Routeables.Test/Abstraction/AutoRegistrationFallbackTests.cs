using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction.Registrations;

namespace Routeables.Tests.Abstraction;

public class AutoRegistrationFallbackTests
{
    // The host runs the module constructors of the assemblies that reference Routeables before it applies the routes. A runtime that skips module
    // initializers (.NET MAUI for Android did) would otherwise leave the routes unregistered. It must not fail, and may be called again.
    [Fact]
    public void Apply_can_run_the_module_constructors_and_be_called_again()
    {
        var first = new ServiceCollection();
        var second = new ServiceCollection();

        RouteableAutoRegistration.Apply(first);
        RouteableAutoRegistration.Apply(second);

        Assert.NotNull(first);
    }
}
