using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Test.Abstraction;

public class SingletonRegistrationTests
{
    public sealed class Model : IDisposable
    {
        public int Disposed { get; private set; }
        public void Dispose() => Disposed++;
    }

    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.RegisterRoute("/single", (_, _) => new Model(), ServiceLifetime.Singleton);
        return services;
    }

    private static object Resolve(IServiceProvider sp)
        => sp.GetRequiredKeyedService<Func<IServiceProvider, IRouteableArgs, object>>("/single")(sp, new RouteableArgs());

    [Fact]
    public void A_singleton_is_created_once_and_disposed_with_the_provider()
    {
        var provider = Services().BuildServiceProvider();

        var first = (Model)Resolve(provider);
        var second = (Model)Resolve(provider);

        Assert.Same(first, second);
        Assert.Equal(0, first.Disposed);

        provider.Dispose();

        Assert.Equal(1, first.Disposed);
    }

    [Fact]
    public void Two_providers_built_from_the_same_collection_do_not_share_their_singletons()
    {
        var services = Services();
        using var one = services.BuildServiceProvider();
        using var two = services.BuildServiceProvider();

        Assert.False(ReferenceEquals(Resolve(one), Resolve(two)));
    }

    public sealed class View : IDisposable
    {
        public int Disposed { get; private set; }
        public void Dispose() => Disposed++;
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Transient)]
    public void A_view_resolved_again_is_disposed_once(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        services.RegisterRouteableArgsFactory(() => new RouteableArgs());
        services.RegisterRoute<View, Model, string>(ServiceLifetime.Transient, lifetime, "/v");
        var provider = services.BuildServiceProvider();

        var first = (View)provider.GetView("/v")!;
        for (var i = 0; i < 3; i++) provider.GetView("/v");
        var last = (View)provider.GetView("/v")!;

        provider.Dispose();

        Assert.Equal(1, first.Disposed);
        Assert.Equal(1, last.Disposed);
    }
}
