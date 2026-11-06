using Microsoft.Extensions.DependencyInjection;
using Routeables.AspNetCore;
using Routeables.Core;
using Routeables.Services;

namespace Routeables.Tests.Hosts;

public class AspNetCoreThemeServiceTests
{
    [Fact]
    public void Starts_with_the_default_theme()
        => Assert.Equal(RouteableRequestedTheme.Default, new RouteableRequestThemeService().Requested);

    [Fact]
    public void Request_keeps_the_theme_and_raises_the_event_once_per_change()
    {
        var service = new RouteableRequestThemeService();
        var seen = new List<RouteableRequestedTheme>();
        service.RequestedThemeChanged += (_, theme) => seen.Add(theme);

        service.Request(RouteableRequestedTheme.Dark);
        service.Request(RouteableRequestedTheme.Dark);
        service.Request(RouteableRequestedTheme.Light);

        Assert.Equal(RouteableRequestedTheme.Light, service.Requested);
        Assert.Equal(2, seen.Count);
        Assert.Equal(RouteableRequestedTheme.Dark, seen[0]);
        Assert.Equal(RouteableRequestedTheme.Light, seen[1]);
    }

    [Fact]
    public void AddRouteablesAspNetCore_registers_one_service_per_scope_under_both_types()
    {
        var services = new ServiceCollection();
        services.AddRouteablesAspNetCore();
        using var provider = services.BuildServiceProvider();

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        var asInterface = first.ServiceProvider.GetRequiredService<IRouteableRequestedThemeService>();
        Assert.Same(asInterface, first.ServiceProvider.GetRequiredService<RouteableRequestThemeService>());

        // A visitor's request does not change the theme of another one.
        asInterface.Request(RouteableRequestedTheme.Dark);
        Assert.Equal(RouteableRequestedTheme.Default, second.ServiceProvider.GetRequiredService<RouteableRequestThemeService>().Requested);
    }
}
