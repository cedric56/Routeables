using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Injections;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Core;
using Routeables.Routing;

namespace Routeables.Tests.Hosts;

/// <summary>
/// An application can build the container of a desktop host with its own container library. DryIoc, Autofac and the others are not part of this
/// repository: the provider below is a stand-in that is not a <c>ServiceProvider</c> (it only wraps one), so that nothing in Routeables can rely on
/// the concrete type of the default container.
/// </summary>
public class CustomContainerTests
{
    public sealed class PageView : IRouteableFor { public object? Context { get; set; } }
    public sealed class PageViewModel(string name) { public string Name { get; } = name; }
    public sealed class Clock { }

    /// <summary>A provider of another library: keyed services and scopes, and nothing of the type of <c>ServiceProvider</c>.</summary>
    private sealed class ForeignProvider(IServiceProvider inner) : IServiceProvider, IKeyedServiceProvider, IServiceScopeFactory, IDisposable
    {
        public int Resolutions;

        public object? GetService(Type serviceType)
        {
            Resolutions++;
            return serviceType == typeof(IServiceScopeFactory) ? this : inner.GetService(serviceType);
        }

        public object? GetKeyedService(Type serviceType, object? serviceKey)
        {
            Resolutions++;
            return ((IKeyedServiceProvider)inner).GetKeyedService(serviceType, serviceKey);
        }

        public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
            => GetKeyedService(serviceType, serviceKey) ?? throw new InvalidOperationException($"No service for {serviceType}/{serviceKey}.");

        public IServiceScope CreateScope() => new ForeignScope(inner.CreateScope());

        public void Dispose() => (inner as IDisposable)?.Dispose();

        private sealed class ForeignScope(IServiceScope scope) : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = new ForeignProvider(scope.ServiceProvider);

            public void Dispose() => scope.Dispose();
        }
    }

    /// <summary>The factory of a container library: <c>CreateBuilder</c> gets the collection, <c>CreateServiceProvider</c> makes the provider.</summary>
    private sealed class ForeignFactory : IServiceProviderFactory<IServiceCollection>
    {
        public int Builders { get; private set; }
        public ForeignProvider? Created { get; private set; }

        public IServiceCollection CreateBuilder(IServiceCollection services)
        {
            Builders++;
            return services;
        }

        public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
            => Created = new ForeignProvider(containerBuilder.BuildServiceProvider());
    }

    [Fact]
    public async Task The_host_builds_its_container_with_the_factory_the_application_chose_and_routes_through_it()
    {
        var factory = new ForeignFactory();
        var services = new ServiceCollection();
        services.AddRouteableHost(Runtime.Avalonia);
        services.RegisterRoute<PageView, PageViewModel>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/page/{name}");
        services.AddTransient<PageViewModel>();
        services.UseRouteablesContainer(factory);

        var provider = services.BuildRouteableProvider();

        Assert.Equal(1, factory.Builders);
        Assert.Same(factory.Created, provider);
        Assert.IsNotType<ServiceProvider>(provider);

        var router = provider.GetRequiredService<IRouter>();
        var result = await router.GoToAsync("/page/Ada", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Exception?.Message);
        Assert.Equal("Ada", Assert.IsType<PageViewModel>(router.CurrentViewModel).Name);
        Assert.True(factory.Created!.Resolutions > 0);
    }

    [Fact]
    public void A_builder_function_can_register_in_the_container_after_the_collection()
    {
        var services = new ServiceCollection();
        services.AddRouteableHost(Runtime.Wpf);
        services.UseRouteablesContainer(collection =>
        {
            collection.AddSingleton<Clock>();   // what a library would register on its own container
            return new ForeignProvider(collection.BuildServiceProvider());
        });

        var provider = services.BuildRouteableProvider();

        Assert.IsNotType<ServiceProvider>(provider);
        Assert.NotNull(provider.GetService<Clock>());
        Assert.NotNull(provider.GetService<IRouter>());
    }

    [Fact]
    public void The_last_choice_wins_and_without_one_the_default_container_is_built()
    {
        var first = new ForeignFactory();
        var second = new ForeignFactory();
        var services = new ServiceCollection();
        services.AddRouteableHost(Runtime.Wpf);
        services.UseRouteablesContainer(first);
        services.UseRouteablesContainer(second);

        services.BuildRouteableProvider();

        Assert.Equal(0, first.Builders);
        Assert.Equal(1, second.Builders);

        var plain = new ServiceCollection();
        plain.AddRouteableHost(Runtime.Wpf);
        Assert.IsType<ServiceProvider>(plain.BuildRouteableProvider());
    }

    [Fact]
    public void A_missing_factory_is_refused()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.UseRouteablesContainer<IServiceCollection>(null!));
        Assert.Throws<ArgumentNullException>(() => services.UseRouteablesContainer((Func<IServiceCollection, IServiceProvider>)null!));
    }
}
