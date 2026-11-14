using Routeables.Services;

namespace Routeables.Tests.Core;

public class RequestedThemeCurrentTests
{
    private sealed class Legacy : IRouteableRequestedThemeService
    {
        public void Request(RouteableRequestedTheme theme) { }
    }

    private sealed class Dark : IRouteableRequestedThemeService
    {
        public void Request(RouteableRequestedTheme theme) { }
        public RouteableRequestedTheme Current => RouteableRequestedTheme.Dark;
    }

    // An implementation written before Current existed still compiles, and says it cannot tell.
    [Fact]
    public void A_service_that_does_not_override_Current_says_Default()
    {
        IRouteableRequestedThemeService service = new Legacy();
        Assert.Equal(RouteableRequestedTheme.Default, service.Current);
    }

    [Fact]
    public void A_service_can_say_which_theme_it_shows()
    {
        IRouteableRequestedThemeService service = new Dark();
        Assert.Equal(RouteableRequestedTheme.Dark, service.Current);
    }
}
