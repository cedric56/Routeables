using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Razor;
using Routeables.Razor.Services;
using Routeables.Services;
using Routeables.Tests.Core;

namespace Routeables.Tests.Razor;

public class UseRouteablesTests
{
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/");

        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    [Fact]
    public void UseRouteablesServer_registers_the_main_scoped_and_runs_the_block()
    {
        var services = new ServiceCollection();
        var blockRan = false;

        services.UseRouteablesServer<Tests.Core.FakeMainViewModel>(r => blockRan = ReferenceEquals(r, services));

        Assert.True(blockRan);
        Assert.Equal(ServiceLifetime.Scoped, services.Last(d => d.ServiceType == typeof(IMainViewModel)).Lifetime);
    }

    [Fact]
    public void Each_Razor_host_registers_its_own_runtime()
    {
        foreach (var (use, expected) in new (Action<IServiceCollection>, Runtime)[]
        {
            (s => s.UseRouteablesServer<FakeMainViewModel>(), Runtime.BlazorServer),
            (s => s.UseRouteablesWebAssembly<FakeMainViewModel>(), Runtime.BlazorWebAssembly),
            (s => s.UseRouteablesHybrid<FakeMainViewModel>(), Runtime.MauiHybrid),
        })
        {
            var services = new ServiceCollection();
            use(services);
            using var provider = services.BuildServiceProvider();
            Assert.Equal(expected, provider.GetRequiredService<IRuntime>().Current);
        }
    }

    [Fact]
    public void UseRouteablesServer_refuses_a_main_registered_as_singleton_before_it()
    {
        var services = new ServiceCollection();
        services.RegisterMainViewModel<Tests.Core.FakeMainViewModel>();     // no host yet: Singleton

        Assert.Throws<InvalidOperationException>(() => services.UseRouteablesServer<FakeMainViewModel>());
    }

    [Fact]
    public async Task UseRouteables_registers_scoped_services_and_the_supplied_circuit_callback()
    {
        var services = new ServiceCollection();
        var callbackCalls = 0;
        Func<IServiceProvider, Task> callback = _ =>
        {
            callbackCalls++;
            return Task.CompletedTask;
        };
        services.AddSingleton<NavigationManager>(new TestNavigationManager());

        services.UseRouteablesServer<FakeMainViewModel>(onCircuitProviderChanged: callback);
        services.UseRouteablesServices();
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var firstRouter = firstScope.ServiceProvider.GetRequiredService<IRouter>();
        Assert.Same(firstRouter, firstScope.ServiceProvider.GetRequiredService<IRouter>());
        Assert.NotSame(firstRouter, secondScope.ServiceProvider.GetRequiredService<IRouter>());
        Assert.IsType<RouteableRequestThemeService>(firstScope.ServiceProvider.GetRequiredService<IRouteableRequestedThemeService>());
        Assert.NotNull(firstScope.ServiceProvider.GetRequiredService<IRouteableNotificationService>());

        var registeredCallback = firstScope.ServiceProvider.GetRequiredService<Func<IServiceProvider, Task>>();
        Assert.Same(callback, registeredCallback);
        await registeredCallback(firstScope.ServiceProvider);
        Assert.Equal(1, callbackCalls);

        var injection = firstScope.ServiceProvider.GetRequiredService<IInjection>();
        Assert.Collection(
            injection.AttributeTypes,
            entry => Assert.Equal(typeof(InjectAttribute), entry.Type),
            entry => Assert.Equal(typeof(ParameterAttribute), entry.Type));
    }

    [Theory]
    [InlineData(RouteableNotificationPosition.TopLeft)]
    [InlineData(RouteableNotificationPosition.TopCenter)]
    [InlineData(RouteableNotificationPosition.TopRight)]
    [InlineData(RouteableNotificationPosition.BottomLeft)]
    [InlineData(RouteableNotificationPosition.BottomCenter)]
    [InlineData(RouteableNotificationPosition.BottomRight)]
    public void UseRouteables_applies_the_notification_position_and_maximum(RouteableNotificationPosition position)
    {
        var services = new ServiceCollection();
        services.AddSingleton<NavigationManager>(new TestNavigationManager());

        services.UseRouteablesServer<FakeMainViewModel>();
        services.UseRouteablesServices(position: position, maxDispayments: 3);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var options = scope.ServiceProvider.GetRequiredService<RouteableNotificationOptions>();
        Assert.Equal(position, options.Position);
        Assert.Equal(3, options.MaxDisplayed);
        var service = scope.ServiceProvider.GetRequiredService<RouteableNotificationService>();
        Assert.Equal(position, service.Position);
        Assert.Same(service, scope.ServiceProvider.GetRequiredService<IRouteableNotificationService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRouteableNotificationService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRouter>());
    }

    [Fact]
    public void UseRouteables_registers_the_router_the_theme_and_the_dialogs_and_notifications()
    {
        var services = new ServiceCollection();
        services.AddSingleton<NavigationManager>(new TestNavigationManager());

        services.UseRouteablesServer<FakeMainViewModel>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRouter>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRouteableRequestedThemeService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRouteableDialogService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRouteableNotificationService>());
    }

    [Fact]
    public void Calling_UseRouteablesServices_again_replaces_the_defaults_without_duplicating_them()
    {
        var services = new ServiceCollection();
        services.AddSingleton<NavigationManager>(new TestNavigationManager());

        services.UseRouteablesServer<FakeMainViewModel>();
        services.UseRouteablesServices(position: RouteableNotificationPosition.TopRight);
        services.UseRouteablesServices(position: RouteableNotificationPosition.BottomCenter);

        Assert.Single(services, d => d.ServiceType == typeof(IRouteableDialogService));
        Assert.Single(services, d => d.ServiceType == typeof(IRouteableNotificationService));
        Assert.Single(services, d => d.ServiceType == typeof(RouteableNotificationOptions));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.Equal(RouteableNotificationPosition.BottomCenter, scope.ServiceProvider.GetRequiredService<RouteableNotificationOptions>().Position);
    }

    [Fact]
    public void UseRouteablesServices_replaces_a_service_by_the_factory_it_is_given()
    {
        var services = new ServiceCollection();
        services.AddSingleton<NavigationManager>(new TestNavigationManager());
        var custom = new RouteableNotificationService(new RouteableNotificationOptions());

        services.UseRouteablesServer<FakeMainViewModel>();
        services.UseRouteablesServices(notificationService: _ => custom);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Same(custom, scope.ServiceProvider.GetRequiredService<IRouteableNotificationService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRouteableDialogService>());
    }
}
