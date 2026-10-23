using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Razor;
using Routeables.Razor.Services;
using Routeables.Services;

namespace Routeables.Tests.Razor;

public class UseRouteablesTests
{
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/");

        protected override void NavigateToCore(string uri, bool forceLoad) { }
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

        services.UseRouteables(Runtime.BlazorServer, onCircuitProviderChanged: callback);
        services.UseRouteablesServices();
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var firstRouter = firstScope.ServiceProvider.GetRequiredService<IRouter>();
        Assert.Same(firstRouter, firstScope.ServiceProvider.GetRequiredService<IRouter>());
        Assert.NotSame(firstRouter, secondScope.ServiceProvider.GetRequiredService<IRouter>());
        Assert.IsType<RouteableRequestThemeService>(firstScope.ServiceProvider.GetRequiredService<IRouteableRequestedThemeService>());
        Assert.NotNull(firstScope.ServiceProvider.GetRequiredService<IRouteableNotificationService>());
        Assert.NotNull(firstScope.ServiceProvider.GetRequiredService<IRouteableContentDialogService>());
        Assert.NotNull(firstScope.ServiceProvider.GetRequiredService<IRouteableTaskDialogService>());

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

        services.UseRouteables(Runtime.BlazorServer);
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
    public void UseRouteables_alone_registers_the_router_and_the_theme_but_no_dialog_or_notification()
    {
        var services = new ServiceCollection();
        services.AddSingleton<NavigationManager>(new TestNavigationManager());

        services.UseRouteables(Runtime.BlazorServer);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRouter>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRouteableRequestedThemeService>());
        Assert.Null(scope.ServiceProvider.GetService<IRouteableDialogService>());
        Assert.Null(scope.ServiceProvider.GetService<IRouteableContentDialogService>());
        Assert.Null(scope.ServiceProvider.GetService<IRouteableTaskDialogService>());
        Assert.Null(scope.ServiceProvider.GetService<IRouteableNotificationService>());
        Assert.Null(scope.ServiceProvider.GetService<RouteableNotificationOptions>());
    }

    [Fact]
    public void UseRouteablesServices_replaces_a_service_by_the_factory_it_is_given()
    {
        var services = new ServiceCollection();
        services.AddSingleton<NavigationManager>(new TestNavigationManager());
        var custom = new RouteableNotificationService(new RouteableNotificationOptions());

        services.UseRouteables(Runtime.BlazorServer);
        services.UseRouteablesServices(routeableNotificationService: _ => custom);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Same(custom, scope.ServiceProvider.GetRequiredService<IRouteableNotificationService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRouteableDialogService>());
    }
}
