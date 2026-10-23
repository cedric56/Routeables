using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Razor;
using Routeables.Tests.Core;
using Routeables.Routing;

namespace Routeables.Tests.Razor;

/// <summary>Address &lt;-&gt; router synchronisation: <see cref="RouteableUrlSync"/> and the <see cref="RouteableTable"/> it reads.</summary>
public class RouteableUrlSyncTests
{
    private sealed class TestNavigationManager : NavigationManager
    {
        public List<(string Uri, bool Replace)> Calls { get; } = [];

        public TestNavigationManager(string uri = "http://localhost/", string baseUri = "http://localhost/")
            => Initialize(baseUri, uri);

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            // Recorded as the address inside the application ("/home"), whatever the base path is.
            var absolute = ToAbsoluteUri(uri).ToString();
            Calls.Add(("/" + ToBaseRelativePath(absolute), options.ReplaceHistoryEntry));
            Uri = absolute;
            NotifyLocationChanged(isInterceptedLink: false);
        }

        /// <summary>What the browser does when the user clicks Back or types an address (relative to the base path).</summary>
        public void BrowserGoesTo(string path)
        {
            Uri = ToAbsoluteUri(path.TrimStart('/')).ToString();
            NotifyLocationChanged(isInterceptedLink: false);
        }
    }

    public sealed class HomeView : IRouteableFor { public object? Context { get; set; } }
    public sealed class HomeVm;

    public sealed class CustomerView : IRouteableFor { public object? Context { get; set; } }
    public sealed class CustomerVm(int id) : IRouteable
    {
        public int Id { get; } = id;
        public static bool Allow = true;
        public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(Allow);
    }

    public sealed class PlainView : IRouteableFor { public object? Context { get; set; } }
    public sealed class PlainVm;

    private sealed class Fixture : IDisposable
    {
        public ServiceProvider Provider { get; }
        public TestNavigationManager Routing { get; }
        public IRouter Router { get; }
        public RouteableUrlSync Sync { get; }

        public Fixture(string uri = "http://localhost/")
        {
            CustomerVm.Allow = true;
            var services = new ServiceCollection();
            services.RegisterRouting();
            services.RegisterRoute<HomeView, HomeVm>(route: "/home");
            services.RegisterRoute<CustomerView, CustomerVm>(route: "/customers/{id:int}");
            services.RegisterRoute<PlainView, PlainVm>();            // no URL
            Routing = new TestNavigationManager(uri);
            services.AddSingleton<NavigationManager>(Routing);
            Provider = services.BuildServiceProvider();
            Router = Provider.GetRequiredService<IRouter>();
            Sync = new RouteableUrlSync(Routing, Router, Provider);
        }

        public Task<RouteableResult> Home() =>
            Router.GoToRouteAsync<HomeVm>("/home", template: "/home", cancellationToken: TestContext.Current.CancellationToken);

        public Task<RouteableResult> Customer(int id) =>
            Router.GoToRouteAsync<CustomerVm>($"/customers/{id}", template: "/customers/{id:int}", cancellationToken: TestContext.Current.CancellationToken);

        // The address handler is async void (an event handler): wait for its effect.
        public static async Task Until(Func<bool> condition)
        {
            for (var i = 0; i < 200 && !condition(); i++)
                await Task.Delay(10);
            Assert.True(condition(), "condition not reached");
        }

        public void Dispose()
        {
            Sync.Dispose();
            Provider.Dispose();
        }
    }

    [Fact]
    public async Task Reusing_the_same_arguments_does_not_leak_one_routing_into_the_next()
    {
        using var f = new Fixture();
        var args = RouteableArgs.Create();

        var first = await f.Router.GoToRouteAsync<CustomerVm>("/customers/1", args, "/customers/{id:int}", TestContext.Current.CancellationToken);
        var second = await f.Router.GoToRouteAsync<CustomerVm>("/customers/2", args, "/customers/{id:int}", TestContext.Current.CancellationToken);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Empty(args.ViewModelParameters);   // the caller's instance is a model, never filled
        Assert.Null(args.Route);
    }

    [Theory]
    [InlineData("/customers/abc", "\"abc\" is not a valid int for {id}")]
    [InlineData("/customers", "the required parameter {id} is missing")]
    [InlineData("/clients/42", "segment 1 is \"clients\", expected \"customers\"")]
    [InlineData("/customers/42/orders", "the route has 3 segment(s), the template only 2")]
    public async Task A_route_that_does_not_fit_its_template_says_why(string route, string reason)
    {
        using var f = new Fixture();

        var result = await f.Router.GoToRouteAsync<CustomerVm>(route, template: "/customers/{id:int}", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains(route, result.Exception!.Message);
        Assert.Contains(reason, result.Exception.Message);
    }

    [Fact]
    public async Task A_registered_route_typed_in_the_address_becomes_the_displayed_page()
    {
        CustomerVm.Allow = true;
        var main = new FakeMainRouteables();
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<CustomerView, CustomerVm>(route: "/customers/{id:int}");
        var routing = new TestNavigationManager("http://localhost/customers/7");
        services.AddSingleton<NavigationManager>(routing);
        services.AddSingleton<IMainRouteables>(main);
        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();
        using var sync = new RouteableUrlSync(routing, router, provider);

        var chosen = await sync.StartAsync();

        Assert.True(chosen);                                   // the layout then keeps this page instead of the default one
        Assert.IsType<CustomerView>(main.CurrentView);         // and displays it
        Assert.Same(router.CurrentView, main.CurrentView);
    }

    [Fact]
    public void Registering_a_url_route_fills_the_route_table_and_other_routes_stay_out()
    {
        using var f = new Fixture();

        var table = f.Provider.GetRequiredService<RouteableTable>();

        Assert.Equal(["/home", "/customers/{id:int}"], table.Entries.Select(e => e.Template).ToList());
    }

    [Fact]
    public void Registering_the_same_url_twice_keeps_one_entry()
    {
        var services = new ServiceCollection();
        services.RegisterRoute<HomeView, HomeVm>(route: "/home");
        services.RegisterRoute<HomeView, HomeVm>(route: "/HOME");

        using var provider = services.BuildServiceProvider();

        Assert.Single(provider.GetRequiredService<RouteableTable>().Entries);
    }

    [Fact]
    public async Task A_router_routing_updates_the_address_with_a_new_browser_entry()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();

        await f.Home();
        await f.Customer(42);

        Assert.Equal([("/home", true), ("/customers/42", false)], f.Routing.Calls);   // the first page replaces the initial entry
        Assert.Equal("http://localhost/customers/42", f.Routing.Uri);
    }

    [Fact]
    public async Task A_route_without_a_leading_slash_is_shown_with_one_and_found_from_the_address()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<HomeView, HomeVm>(route: "about");
        var routing = new TestNavigationManager();
        services.AddSingleton<NavigationManager>(routing);
        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();
        using var sync = new RouteableUrlSync(routing, router, provider);
        await sync.StartAsync();

        await router.GoToRouteAsync<HomeVm>("about", template: "about", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([("/about", true)], routing.Calls);
        Assert.Equal("http://localhost/about", routing.Uri);

        // and the other way round: a reload on /about finds the route again
        var other = new TestNavigationManager("http://localhost/about");
        var scope = provider.CreateScope();
        using var again = new RouteableUrlSync(other, scope.ServiceProvider.GetRequiredService<IRouter>(), scope.ServiceProvider);
        Assert.True(await again.StartAsync());
    }

    [Fact]
    public async Task A_view_only_route_such_as_a_Routeable_view_is_synchronised_too()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<PlainView>("about");                   // what [Routeable("about")] on a view generates
        services.RegisterRoute("help", (sp, args) => new PlainView());
        var routing = new TestNavigationManager("http://localhost/about");
        services.AddSingleton<NavigationManager>(routing);
        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();
        using var sync = new RouteableUrlSync(routing, router, provider);

        Assert.True(await sync.StartAsync());
        Assert.IsType<PlainView>(router.CurrentView);

        routing.BrowserGoesTo("/help");
        await Fixture.Until(() => router.History.Count == 2);
        Assert.Equal(["about", "help"], provider.GetRequiredService<RouteableTable>().Entries.Select(e => e.Template).ToList());
    }

    [Fact]
    public async Task Going_back_in_the_router_replaces_the_address()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();
        await f.Home();
        await f.Customer(1);
        f.Routing.Calls.Clear();

        await f.Router.GoBackAsync(TestContext.Current.CancellationToken);

        Assert.Equal([("/home", true)], f.Routing.Calls);
    }

    [Fact]
    public async Task Two_quick_address_changes_are_applied_one_after_the_other()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();
        await f.Home();

        f.Routing.BrowserGoesTo("/customers/1");
        f.Routing.BrowserGoesTo("/customers/2");

        await Fixture.Until(() => f.Router.CurrentViewModel is CustomerVm { Id: 2 });
        Assert.Equal(3, f.Router.History.Count);                        // home, 1, 2: none lost, none duplicated
    }

    [Fact]
    public async Task A_page_without_a_route_puts_the_address_back_to_the_root()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();
        await f.Home();
        f.Routing.Calls.Clear();

        await f.Router.GoToRouteAsync<PlainVm>(
            new RouteableEntry<PlainVm>("plain"), TestContext.Current.CancellationToken);

        Assert.Equal([("/", false)], f.Routing.Calls);      // a new browser entry: Back returns to /home
        Assert.Equal("http://localhost/", f.Routing.Uri);
    }

    public sealed class SlugView : IRouteableFor { public object? Context { get; set; } }
    public sealed class SlugVm(string slug) { public string Slug { get; } = slug; }

    [Theory]
    [InlineData("//attacker.example")]
    [InlineData("///attacker.example/x")]
    [InlineData("/\\attacker.example")]
    public async Task An_address_never_becomes_a_link_to_another_site(string route)
    {
        // A route written by the application from user input (a returnUrl, a search box) must not turn into "//host": the browser
        // reads that as a protocol-relative link and leaves the application.
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<HomeView, HomeVm>(route: "/home");
        services.RegisterRoute<SlugView, SlugVm>(route: "{slug}");
        var routing = new TestNavigationManager("http://localhost/home");
        services.AddSingleton<NavigationManager>(routing);
        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();
        using var sync = new RouteableUrlSync(routing, router, provider);
        await sync.StartAsync();
        routing.Calls.Clear();

        await router.GoToAsync(route, cancellationToken: TestContext.Current.CancellationToken);

        foreach (var call in routing.Calls)
        {
            Assert.True(call.Uri.StartsWith('/'), $"routed to {call.Uri}");
            Assert.False(call.Uri.StartsWith("//") || call.Uri.StartsWith("/\\"), $"routed to {call.Uri}");
        }
    }

    [Fact]
    public async Task A_page_without_a_route_does_not_touch_an_address_that_is_already_the_root()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();

        await f.Router.GoToRouteAsync<PlainVm>(
            new RouteableEntry<PlainVm>("plain"), TestContext.Current.CancellationToken);

        Assert.Empty(f.Routing.Calls);
    }

    [Fact]
    public async Task The_browser_buttons_move_between_a_routed_page_and_one_without_a_route()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();
        await f.Home();
        await f.Router.GoToRouteAsync<PlainVm>(
            new RouteableEntry<PlainVm>("plain"), TestContext.Current.CancellationToken);

        f.Routing.BrowserGoesTo("/home");                   // Back
        await Fixture.Until(() => f.Router.CurrentViewModel is HomeVm);
        f.Routing.BrowserGoesTo("/");                       // Forward
        await Fixture.Until(() => f.Router.CurrentViewModel is PlainVm);

        Assert.Equal(2, f.Router.History.Count);               // moved inside the history, nothing pushed
    }

    [Fact]
    public async Task The_current_address_is_routed_to_when_it_names_a_route()
    {
        using var f = new Fixture("http://localhost/customers/7");

        var handled = await f.Sync.StartAsync();

        Assert.True(handled);
        Assert.Equal(7, Assert.IsType<CustomerVm>(f.Router.CurrentViewModel).Id);
        Assert.Empty(f.Routing.Calls);       // the address already was right
    }

    [Fact]
    public async Task An_address_that_matches_no_route_is_left_to_the_caller()
    {
        using var f = new Fixture("http://localhost/nowhere");

        var handled = await f.Sync.StartAsync();

        Assert.False(handled);
        Assert.Null(f.Router.CurrentViewModel);
    }

    [Fact]
    public async Task An_address_change_routes_the_router()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();
        await f.Home();

        f.Routing.BrowserGoesTo("/customers/9");

        await Fixture.Until(() => f.Router.CurrentViewModel is CustomerVm);
        Assert.Equal(9, ((CustomerVm)f.Router.CurrentViewModel!).Id);
        Assert.Equal(2, f.Router.History.Count);
    }

    [Fact]
    public async Task The_browser_back_button_uses_the_router_history_instead_of_pushing_again()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();
        await f.Home();
        await f.Customer(1);

        f.Routing.BrowserGoesTo("/home");

        await Fixture.Until(() => f.Router.CurrentViewModel is HomeVm);
        Assert.Equal(2, f.Router.History.Count);
        Assert.Equal(0, f.Router.History.CurrentIndex);
    }

    [Fact]
    public async Task A_refused_routing_puts_the_previous_address_back()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();
        await f.Customer(1);
        f.Routing.Calls.Clear();
        CustomerVm.Allow = false;                    // the guard of the page being left says no

        f.Routing.BrowserGoesTo("/home");

        await Fixture.Until(() => f.Routing.Calls.Count > 0);
        Assert.Equal([("/customers/1", true)], f.Routing.Calls);
        Assert.IsType<CustomerVm>(f.Router.CurrentViewModel);
    }

    public sealed class BoomView : IRouteableFor { public object? Context { get; set; } }
    public sealed class BoomVm : IRouteable
    {
        public BoomVm() => throw new InvalidOperationException("cannot be built");
        public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class Sink : IRouteableErrorSink
    {
        public List<string> Sources { get; } = [];
        public void Report(string source, Exception exception) => Sources.Add(source);
    }

    [Fact]
    public async Task An_address_the_page_cannot_be_built_for_is_reported_and_the_previous_address_is_put_back()
    {
        CustomerVm.Allow = true;
        var sink = new Sink();
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<HomeView, HomeVm>(route: "/home");
        services.RegisterRoute<BoomView, BoomVm>(route: "/boom");
        services.AddSingleton<IRouteableErrorSink>(sink);
        var routing = new TestNavigationManager();
        services.AddSingleton<NavigationManager>(routing);
        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();
        using var sync = new RouteableUrlSync(routing, router, provider);
        await sync.StartAsync();
        await router.GoToRouteAsync<HomeVm>("/home", template: "/home", cancellationToken: TestContext.Current.CancellationToken);
        routing.Calls.Clear();

        routing.BrowserGoesTo("/boom");

        await Fixture.Until(() => routing.Calls.Count > 0);
        Assert.Equal([("/home", true)], routing.Calls);
        Assert.Contains(sink.Sources, s => s.StartsWith("RouteableUrlSync.Open"));
        Assert.IsType<HomeVm>(router.CurrentViewModel);
    }

    [Fact]
    public async Task After_Dispose_neither_side_follows_the_other()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();
        f.Sync.Dispose();

        await f.Home();
        f.Routing.BrowserGoesTo("/customers/3");
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Empty(f.Routing.Calls);
        Assert.IsType<HomeVm>(f.Router.CurrentViewModel);
    }
}