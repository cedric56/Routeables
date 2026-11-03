using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Core;

namespace Routeables.Tests.Core;

public class RouteableHostRegistrationTests
{
    [Fact]
    public void A_host_gets_the_router_the_runtime_and_the_shared_attribute()
    {
        var services = new ServiceCollection();
        services.AddRouteableHost(Runtime.Uno, new RouteablePropertyEntry(typeof(InjectPropertyAttribute)));

        var provider = services.BuildRouteableProvider();
        var attributes = provider.GetRequiredService<IInjection>().AttributeTypes;

        Assert.Equal(Runtime.Uno, provider.GetRequiredService<IRuntime>().Current);
        Assert.Equal([typeof(RouteableParameterAttribute), typeof(InjectPropertyAttribute)], attributes.Select(a => a.Type));
        Assert.False(attributes[0].FromContainer);
        Assert.NotNull(provider.GetService<IRouteableArgsFactory>());
    }
}
