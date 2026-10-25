using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Services;

namespace Routeables.Tests.Services;

/// <summary>The addresses that come from outside the application, and the history that is saved and restored.</summary>
public class RouteableDeepLinksTests
{
    public sealed class LinkFirstView : IRouteableFor { public object? Context { get; set; } }
    public sealed class LinkFirstVm;
    public sealed class LinkSecondView : IRouteableFor { public object? Context { get; set; } }
    public sealed class LinkSecondVm;

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<LinkFirstView, LinkFirstVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/link-first");
        services.RegisterRoute<LinkSecondView, LinkSecondVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/link-second");
        services.AddTransient<LinkFirstVm>();
        services.AddTransient<LinkSecondVm>();
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData("https://example.com/orders/42?x=1#top", "/orders/42?x=1")]
    [InlineData("HTTPS://example.com/orders", "/orders")]
    [InlineData("myapp://orders/42?x=1", "/orders/42?x=1")]
    [InlineData("myapp:///orders/42", "/orders/42")]
    [InlineData("myapp://orders", "/orders")]
    public void An_address_gives_its_route(string address, string expected)
    {
        Assert.True(RouteableDeepLinks.TryGetRoute(new Uri(address), out var route));
        Assert.Equal(expected, route);
    }

    [Theory]
    [InlineData("myapp://")]
    [InlineData("https://example.com/")]
    public void An_address_with_no_path_has_no_route(string address)
        => Assert.False(RouteableDeepLinks.TryGetRoute(new Uri(address), out _));

    [Fact]
    public void A_relative_or_missing_address_has_no_route()
    {
        Assert.False(RouteableDeepLinks.TryGetRoute(new Uri("/a", UriKind.Relative), out _));
        Assert.False(RouteableDeepLinks.TryGetRoute(null, out _));
    }

    [Fact]
    public void Only_the_accepted_schemes_give_a_route()
    {
        string[] schemes = ["myapp", "https"];

        Assert.True(RouteableDeepLinks.TryGetRoute(new Uri("MYAPP://orders"), schemes, out _));
        Assert.False(RouteableDeepLinks.TryGetRoute(new Uri("ftp://host/orders"), schemes, out _));
    }

    [Fact]
    public async Task An_address_goes_to_its_route()
    {
        using var provider = Build();

        var result = await RouteableDeepLinks.GoToAsync(provider, new Uri("myapp://link-second"), ["myapp"], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.IsType<LinkSecondVm>(provider.GetRequiredService<IRouter>().CurrentViewModel);
    }

    [Fact]
    public async Task A_refused_scheme_or_an_unknown_route_is_a_failed_result()
    {
        using var provider = Build();

        var scheme = await RouteableDeepLinks.GoToAsync(provider, new Uri("other://link-second"), ["myapp"], TestContext.Current.CancellationToken);
        var unknown = await RouteableDeepLinks.GoToAsync(provider, new Uri("myapp://nowhere"), ["myapp"], TestContext.Current.CancellationToken);

        Assert.False(scheme.Success);
        Assert.IsType<ArgumentException>(scheme.Exception);
        Assert.False(unknown.Success);
    }

    [Fact]
    public async Task The_history_is_captured_up_to_the_current_page()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var ct = TestContext.Current.CancellationToken;
        await router.GoToAsync("/link-first", cancellationToken: ct);
        await router.GoToAsync("/link-second", cancellationToken: ct);

        Assert.Equal(new[] { "/link-first", "/link-second" }, RouteableState.Capture(router).ToArray());

        await router.GoBackAsync(ct);

        Assert.Equal(new[] { "/link-first" }, RouteableState.Capture(router).ToArray());
    }

    [Fact]
    public async Task A_captured_history_is_restored_in_order()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        var restored = await RouteableState.RestoreAsync(router, ["/link-first", "/link-second"], TestContext.Current.CancellationToken);

        Assert.True(restored);
        Assert.IsType<LinkSecondVm>(router.CurrentViewModel);
        Assert.Equal(2, router.History.Entries.Count);
    }

    [Fact]
    public async Task A_route_that_cannot_be_reached_ends_the_restoration()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        var restored = await RouteableState.RestoreAsync(router, ["/link-first", "/nowhere", "/link-second"], TestContext.Current.CancellationToken);

        Assert.False(restored);
        Assert.IsType<LinkFirstVm>(router.CurrentViewModel);
    }

    [Fact]
    public async Task An_empty_list_changes_nothing()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/link-first", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(await RouteableState.RestoreAsync(router, [], TestContext.Current.CancellationToken));
        Assert.IsType<LinkFirstVm>(router.CurrentViewModel);
    }

    [Fact]
    public void The_routes_survive_the_text_they_are_kept_in()
    {
        string[] routes = ["/a?x=1&y=2", "/b c\nd"];

        Assert.Equal(routes, RouteableState.Deserialize(RouteableState.Serialize(routes)).ToArray());
        Assert.Empty(RouteableState.Deserialize(null));
        Assert.Empty(RouteableState.Deserialize(string.Empty));
    }

    public sealed class KeepingView : IRouteableFor { public object? Context { get; set; } }
    public sealed class KeepingVm : IRouteableState
    {
        public string Draft { get; set; } = "";
        public void SaveState(IDictionary<string, string> state) => state["draft"] = Draft;
        public void LoadState(IReadOnlyDictionary<string, string> state) => Draft = state["draft"];
    }

    [Fact]
    public async Task The_values_of_a_view_model_come_back_with_its_page()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<KeepingView, KeepingVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/keeping");
        services.AddTransient<KeepingVm>();
        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();
        var ct = TestContext.Current.CancellationToken;
        await router.GoToAsync("/keeping", cancellationToken: ct);
        ((KeepingVm)router.CurrentViewModel!).Draft = "a = b | c\nd";

        var text = RouteableState.Serialize(RouteableState.CaptureSnapshots(router));
        await RouteableState.RestoreSnapshotsAsync(router, RouteableState.DeserializeSnapshots(text), ct);

        Assert.Equal("a = b | c\nd", ((KeepingVm)router.CurrentViewModel!).Draft);
        Assert.Equal(new[] { "/keeping" }, RouteableState.Deserialize(text).ToArray());
    }

    [Fact]
    public void A_route_is_remembered_by_a_guard_and_taken_back_once()
    {
        var returns = new RouteableReturn();
        Assert.Null(returns.Take());

        returns.Remember("/orders/42");

        Assert.Equal("/orders/42", returns.Pending);
        Assert.Equal("/orders/42", returns.Take());
        Assert.Null(returns.Take());
    }

    [Fact]
    public async Task A_guard_that_redirects_keeps_the_route_it_turned_away()
    {
        var returns = new RouteableReturn();
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.AddSingleton(returns);
        services.AddSingleton<IRouteGuard>(new SignInGuard(returns));
        services.RegisterRoute<LinkFirstView, LinkFirstVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/link-first");
        services.RegisterRoute<LinkSecondView, LinkSecondVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/link-second");
        services.AddTransient<LinkFirstVm>();
        services.AddTransient<LinkSecondVm>();
        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();

        await router.GoToAsync("/link-second", cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<LinkFirstVm>(router.CurrentViewModel);
        Assert.Equal("/link-second", returns.Take());
    }

    private sealed class SignInGuard(RouteableReturn returns) : IRouteGuard
    {
        public Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken)
            => Task.FromResult(change.To.Args.Route == "/link-second" ? returns.RedirectFrom(change, "/link-first") : RouteGuardDecision.Allow);
    }

    [Fact]
    public async Task The_inbox_follows_what_was_pushed_before_the_first_page_once_it_is_shown()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var inbox = new RouteableLinkInbox(["myapp"]);
        inbox.Push(new Uri("myapp://link-second"));
        inbox.Attach(provider);
        await Task.Delay(150, TestContext.Current.CancellationToken);
        Assert.Null(router.CurrentViewModel);

        await router.GoToAsync("/link-first", cancellationToken: TestContext.Current.CancellationToken);

        for (var i = 0; i < 200 && router.CurrentViewModel is not LinkSecondVm; i++)
            await Task.Delay(10, TestContext.Current.CancellationToken);
        Assert.IsType<LinkSecondVm>(router.CurrentViewModel);
    }
}
