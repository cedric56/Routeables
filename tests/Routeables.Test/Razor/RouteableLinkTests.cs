using Microsoft.AspNetCore.Components;
using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Blazor.Components;
using Routeables.Routing;
namespace Routeables.Tests.Razor;

public class RouteableLinkTests
{
    private sealed class FakeNavigation : NavigationManager
    {
        public FakeNavigation() => Initialize("http://localhost/", "http://localhost/");
        public FakeNavigation(string baseUri) => Initialize(baseUri, baseUri);
        protected override void NavigateToCore(string uri, NavigationOptions options) { }
    }

    public sealed class OrderView : IRouteableFor { public object? Context { get; set; } }
    public sealed class OrderVm(int id) { public int Id { get; } = id; }

    private static ServiceProvider Services(string baseUri = "http://localhost/")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<NavigationManager>(new FakeNavigation(baseUri));
        services.RegisterRouting();
        services.RegisterRouteableArgsFactory(() => RouteableArgs.Create());
        services.RegisterRoute<OrderView, OrderVm>(route: "/orders/{id:int}");
        return services.BuildServiceProvider();
    }

    private static async Task<string> RenderAsync(ServiceProvider provider, Dictionary<string, object?> parameters)
    {
        await using var renderer = new Microsoft.AspNetCore.Components.Web.HtmlRenderer(provider, provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<RouteableLink>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }

    [Fact]
    public async Task It_is_a_real_link_to_the_route()
    {
        await using var provider = Services();

        var html = await RenderAsync(provider, new()
        {
            [nameof(RouteableLink.Route)] = "/orders/42",
            [nameof(RouteableLink.ChildContent)] = (RenderFragment)(b => b.AddContent(0, "Order 42")),
            ["class"] = "order-link",
        });

        Assert.Contains("href=\"http://localhost/orders/42\"", html);
        Assert.Contains("class=\"order-link\"", html);
        Assert.Contains("Order 42", html);
    }

    [Fact]
    public async Task A_click_goes_to_the_route_through_the_router()
    {
        await using var provider = Services();

        await RouteableLink.FollowAsync(provider, "/orders/42", null, null, null);

        var router = provider.GetRequiredService<IRouter>();
        Assert.Equal(42, Assert.IsType<OrderVm>(router.CurrentViewModel).Id);
        Assert.Equal("/orders/42", router.History.Current!.Args.Route);
    }

    [Fact]
    public async Task A_route_that_cannot_be_reached_is_reported_and_does_not_throw()
    {
        await using var provider = Services();

        await RouteableLink.FollowAsync(provider, "/nowhere", null, null, null);

        Assert.Null(provider.GetRequiredService<IRouter>().CurrentViewModel);
    }

    [Fact]
    public async Task Under_a_base_path_the_link_stays_inside_the_application()
    {
        await using var provider = Services("http://localhost/app/");

        var html = await RenderAsync(provider, new() { [nameof(RouteableLink.Route)] = "/orders/42" });

        Assert.Contains("href=\"http://localhost/app/orders/42\"", html);
    }

    [Theory]
    [InlineData("//evil.com", "http://localhost/app/evil.com")]
    [InlineData("/\\evil.com", "http://localhost/app/evil.com")]
    [InlineData("javascript:alert(1)", "http://localhost/app/javascript:alert(1)")]
    public async Task A_route_cannot_make_the_link_leave_the_application(string route, string expected)
    {
        await using var provider = Services("http://localhost/app/");

        var html = await RenderAsync(provider, new() { [nameof(RouteableLink.Route)] = route });

        Assert.Contains($"href=\"{expected}\"", html);
    }
}
