using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Routeables.Abstraction;
using Routeables.Blazor;
using Routeables.Routing;

namespace Routeables.Tests.Razor;

/// <summary>A Back or a Forward the router makes itself (<c>GoBackAsync</c> from a view model) is the same move in the history of the browser.</summary>
public class RouteableUrlSyncHistoryTests
{
    // A browser: the entries of its history, the one shown, and what history.go does.
    private sealed class FakeNavigation : NavigationManager
    {
        private readonly List<string> _entries = ["http://localhost/home"];
        private int _at;

        public List<(string Uri, bool Replace)> Calls { get; } = [];
        public FakeNavigation() => Initialize("http://localhost/", "http://localhost/home");

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            var absolute = ToAbsoluteUri(uri).ToString();
            Calls.Add(("/" + ToBaseRelativePath(absolute), options.ReplaceHistoryEntry));
            if (options.ReplaceHistoryEntry)
                _entries[_at] = absolute;
            else
            {
                _entries.RemoveRange(_at + 1, _entries.Count - _at - 1);
                _entries.Add(absolute);
                _at++;
            }
            Uri = absolute;
            NotifyLocationChanged(isInterceptedLink: false);
        }

        public void Go(int delta)
        {
            _at = Math.Clamp(_at + delta, 0, _entries.Count - 1);
            Uri = _entries[_at];
            NotifyLocationChanged(isInterceptedLink: false);
        }
    }

    // What the browser would be told: every history.go(delta), which moves the fake browser.
    private sealed class FakeScripts(FakeNavigation browser) : IJSRuntime
    {
        public List<int> Moves { get; } = [];
        public bool Broken { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => identifier == "import"
                ? Broken ? throw new InvalidOperationException("JavaScript interop calls cannot be issued at this time.") : new((TValue)(object)new Module(this))
                : default;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);

        private sealed class Module(FakeScripts owner) : IJSObjectReference
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            {
                if (identifier == "go")
                {
                    var delta = (int)args![0]!;
                    owner.Moves.Add(delta);
                    owner.Browser.Go(delta);
                }
                return default;
            }

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
                => InvokeAsync<TValue>(identifier, args);

            public ValueTask DisposeAsync() => default;
        }

        public FakeNavigation Browser { get; } = browser;
    }

    public sealed class HomeView : IRouteableFor { public object? Context { get; set; } }
    public sealed class HomeVm;
    public sealed class DetailView : IRouteableFor { public object? Context { get; set; } }
    public sealed class DetailVm(int id) { public int Id { get; } = id; }

    private sealed class Fixture : IDisposable
    {
        public ServiceProvider Provider { get; }
        public FakeNavigation Navigation { get; } = new();
        public FakeScripts Scripts { get; }
        public IRouter Router { get; }
        public RouteableUrlSync Sync { get; }

        public Fixture(bool withScripts = true)
        {
            Scripts = new FakeScripts(Navigation);
            var services = new ServiceCollection();
            services.RegisterRouting();
            services.RegisterRoute<HomeView, HomeVm>(route: "/home");
            services.RegisterRoute<DetailView, DetailVm>(route: "/detail/{id:int}");
            services.AddSingleton<NavigationManager>(Navigation);
            if (withScripts)
                services.AddSingleton<IJSRuntime>(Scripts);
            Provider = services.BuildServiceProvider();
            Router = Provider.GetRequiredService<IRouter>();
            Sync = new RouteableUrlSync(Navigation, Router, Provider);
        }

        public async Task GoAsync(string route, string template)
        {
            Assert.True((await Router.GoToAsync(route, template: template, cancellationToken: TestContext.Current.CancellationToken)).Success);
        }

        public static async Task Until(Func<bool> condition)
        {
            for (var i = 0; i < 200 && !condition(); i++)
                await Task.Delay(10);
            Assert.True(condition(), "condition not reached");
        }

        public void Dispose() { Sync.Dispose(); Provider.Dispose(); }
    }

    [Fact]
    public async Task A_back_of_the_router_goes_back_in_the_browser_history()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();
        await f.GoAsync("/home", "/home");
        await f.GoAsync("/detail/1", "/detail/{id:int}");     // a push: a new browser entry
        f.Navigation.Calls.Clear();

        await f.Router.GoBackAsync(TestContext.Current.CancellationToken);
        await Fixture.Until(() => f.Scripts.Moves.Count == 1);

        Assert.Equal(-1, f.Scripts.Moves[0]);
        Assert.Empty(f.Navigation.Calls);                       // the address is not replaced: the browser moved itself
    }

    [Fact]
    public async Task A_forward_of_the_router_goes_forward_in_the_browser_history()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();
        await f.GoAsync("/home", "/home");
        await f.GoAsync("/detail/1", "/detail/{id:int}");
        await f.Router.GoBackAsync(TestContext.Current.CancellationToken);
        await Fixture.Until(() => f.Scripts.Moves.Count == 1);

        await f.Router.ForwardAsync(TestContext.Current.CancellationToken);
        await Fixture.Until(() => f.Scripts.Moves.Count == 2);

        Assert.Equal(2, f.Scripts.Moves.Count);
        Assert.Equal(-1, f.Scripts.Moves[0]);
        Assert.Equal(1, f.Scripts.Moves[1]);
    }

    [Fact]
    public async Task After_a_move_to_an_entry_the_browser_is_no_longer_followed_and_the_address_is_replaced()
    {
        using var f = new Fixture();
        await f.Sync.StartAsync();
        await f.GoAsync("/detail/1", "/detail/{id:int}");
        await f.GoAsync("/detail/2", "/detail/{id:int}");
        await f.Router.MoveToAsync(0, TestContext.Current.CancellationToken);   // the router history no longer follows the browser's
        f.Navigation.Calls.Clear();
        f.Scripts.Moves.Clear();

        await f.Router.ForwardAsync(TestContext.Current.CancellationToken);

        Assert.Empty(f.Scripts.Moves);
        Assert.Equal(("/detail/1", true), f.Navigation.Calls.Last());          // replaced, not history.go
    }

    [Fact]
    public async Task Without_scripts_the_address_is_replaced()
    {
        using var f = new Fixture(withScripts: false);
        await f.Sync.StartAsync();
        await f.GoAsync("/home", "/home");
        await f.GoAsync("/detail/1", "/detail/{id:int}");
        f.Navigation.Calls.Clear();

        await f.Router.GoBackAsync(TestContext.Current.CancellationToken);

        Assert.Single(f.Navigation.Calls);
        Assert.Equal(("/home", true), f.Navigation.Calls[0]);
    }

    [Fact]
    public async Task When_the_script_cannot_run_the_address_is_replaced()
    {
        using var f = new Fixture();
        f.Scripts.Broken = true;
        await f.Sync.StartAsync();
        await f.GoAsync("/home", "/home");
        await f.GoAsync("/detail/1", "/detail/{id:int}");
        f.Navigation.Calls.Clear();

        await f.Router.GoBackAsync(TestContext.Current.CancellationToken);
        await Fixture.Until(() => f.Navigation.Calls.Count == 1);

        Assert.Equal(("/home", true), f.Navigation.Calls[0]);
    }
}
