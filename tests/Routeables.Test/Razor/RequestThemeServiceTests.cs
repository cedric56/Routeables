using Routeables.Abstraction;
using Routeables.Razor;
using Routeables.Services;

namespace Routeables.Tests.Razor;

public class RequestThemeServiceTests
{
    [Theory]
    [InlineData(RouteableRequestedTheme.Dark, true)]
    [InlineData(RouteableRequestedTheme.Light, false)]
    [InlineData(RouteableRequestedTheme.Default, false)]
    public void Request_updates_dark_mode_and_notifies_subscribers(RouteableRequestedTheme requested, bool expectedDark)
    {
        var service = new RouteableRequestThemeService();
        var received = new List<RouteableRequestedTheme>();
        service.RequestedThemeChanged += (_, theme) => received.Add(theme);

        service.Request(requested);

        Assert.Equal(expectedDark, service.IsDarkMode);
        Assert.Equal([requested], received);
    }
}
