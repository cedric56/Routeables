using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Abstraction.Registrations;

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
    [Fact]
    public void A_singleton_is_shared_by_the_scopes_of_a_host_that_has_one_provider()
    {
        using var provider = Services().BuildServiceProvider();
        using var alice = provider.CreateScope();
        using var bob = provider.CreateScope();

        Assert.Same(Resolve(alice.ServiceProvider), Resolve(bob.ServiceProvider));
    }

    [Fact]
    public void On_a_scoped_host_a_singleton_page_is_kept_per_scope_and_never_shown_to_another_user()
    {
        // Blazor Server: the routers live in the scope of the circuit, and so does what the page captures from it.
        var services = Services();
        services.AddSingleton(new Routeables.Abstraction.Lifecycle.RouteableHostLifetime(ServiceLifetime.Scoped));
        using var provider = services.BuildServiceProvider();
        var alice = provider.CreateScope();
        using var bob = provider.CreateScope();

        var forAlice = (Model)Resolve(alice.ServiceProvider);
        var forBob = (Model)Resolve(bob.ServiceProvider);

        Assert.NotSame(forAlice, forBob);
        Assert.Same(forAlice, Resolve(alice.ServiceProvider));

        // The page goes away with its circuit.
        alice.Dispose();
        Assert.Equal(1, forAlice.Disposed);
        Assert.Equal(0, forBob.Disposed);
    }
}
