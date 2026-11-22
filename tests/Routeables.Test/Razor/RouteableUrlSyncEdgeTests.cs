using System.Collections.ObjectModel;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NSubstitute;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Blazor;
using Routeables.Routing;
using Routeables.Tests.Pages;

namespace Routeables.Tests.Razor;

/// <summary>
/// The paths of <see cref="RouteableUrlSync"/> where something goes wrong or is not for it: an address of another site, a menu that refuses or fails, a
/// browser that cannot be reached, a script that loads late or never, a router that throws while the page is prerendered.
/// </summary>
public class RouteableUrlSyncEdgeTests
{
    private sealed class FakeNavigation : NavigationManager
    {
        public List<(string Uri, bool Replace)> Calls { get; } = [];
        public bool ThrowOnNavigate { get; set; }

        public FakeNavigation() => Initialize("http://localhost/", "http://localhost/home");

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            if (ThrowOnNavigate)
                throw new InvalidOperationException("the page is only being prerendered");
            var absolute = ToAbsoluteUri(uri).AbsoluteUri;
            Calls.Add(("/" + ToBaseRelativePath(absolute), options.ReplaceHistoryEntry));
            Uri = absolute;
            NotifyLocationChanged(isInterceptedLink: false);
        }

        /// <summary>What Blazor asks the handlers before it moves to an address: false when one of them refuses.</summary>
        public async Task<bool> AskAsync(string absolute, bool intercepted = false)
            => await NotifyLocationChangingAsync(absolute, null, intercepted);

        /// <summary>What the browser does when the user types an address or clicks Back: the address changes and the application is told.</summary>
        public void BrowserGoesTo(string path)
        {
            Uri = ToAbsoluteUri(path.TrimStart('/')).AbsoluteUri;
            NotifyLocationChanged(isInterceptedLink: false);
        }
    }

    private sealed class Menu : IRouteableMenuViewModel
    {
        public ObservableCollection<IRouteableMenuEntry> Entries { get; } = [];
        public IRouteableMenuEntry? SelectedEntry { get; set; }
        public object? CurrentView { get; set; }
        public event EventHandler<object?>? CurrentViewChanged { add { } remove { } }
        public event EventHandler? LockChanged { add { } remove { } }
        public string? CurrentTitle { get; }

        public Func<Task<bool>> Select { get; set; } = () => Task.FromResult(true);

        public Task<bool> SelectEntryAsync(IRouteableMenuEntry entry, CancellationToken cancellationToken = default) => Select();
    }

    private sealed class Errors : IRouteableErrorSink
    {
        public List<Exception> Reported { get; } = [];
        public void Report(string source, Exception exception) { lock (Reported) Reported.Add(exception); }
    }

    private sealed class Scripts : IJSRuntime
    {
        public List<bool> Warn { get; } = [];
        public int Imports { get; private set; }
        public int Disposed { get; private set; }
        public int FailFirst { get; set; }
        public TaskCompletionSource? Gate { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier != "import")
                return default;
            Imports++;
            if (FailFirst > 0)
            {
                FailFirst--;
                throw new InvalidOperationException("JavaScript interop calls cannot be issued at this time.");
            }
            return Gate is { } gate ? Load<TValue>(gate.Task) : new((TValue)(object)new Module(this));
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);

        private async ValueTask<TValue> Load<TValue>(Task gate)
        {
            await gate;
            return (TValue)(object)new Module(this);
        }

        private sealed class Module(Scripts owner) : IJSObjectReference
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            {
                if (identifier == "setWarn") owner.Warn.Add((bool)args![0]!);
                if (identifier == "dispose") owner.Disposed++;
                return default;
            }

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
                => InvokeAsync<TValue>(identifier, args);

            public ValueTask DisposeAsync() => default;
        }
    }

    public sealed class FormVm : IRouteableUnsavedChanges
    {
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public event EventHandler? UnsavedChangesChanged;
        public void Edit(bool dirty) { _dirty = dirty; UnsavedChangesChanged?.Invoke(this, EventArgs.Empty); }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Errors Errors { get; } = new();
        public FakeNavigation Navigation { get; } = new();
        public Menu Menu { get; } = new();
        public ServiceProvider Provider { get; }
        public IRouter Router { get; }
        public RouteableUrlSync Sync { get; }

        public Fixture(Scripts? scripts = null, bool withMenu = false, bool form = false)
        {
            var services = new ServiceCollection();
            services.RegisterRouting();
            services.RegisterRoute<FakeViewFor, FakeViewModel>(route: "/home");
            if (form)
                services.RegisterRoute<SecondViewFor, FormVm>(route: "/form");
            services.AddSingleton<IRouteableErrorSink>(Errors);
            services.AddSingleton<NavigationManager>(Navigation);
            if (scripts is not null)
                services.AddSingleton<IJSRuntime>(scripts);
            if (withMenu)
            {
                var about = Substitute.For<IRouteableMenuEntry>();
                about.Route.Returns("/about");
                Menu.Entries.Add(about);
                services.AddSingleton<IRouteableMenuViewModel>(Menu);
            }
            Provider = services.BuildServiceProvider();
            Router = Provider.GetRequiredService<IRouter>();
            Sync = new RouteableUrlSync(Navigation, Router, Provider);
        }

        public async Task<FormVm> ShowFormAsync()
        {
            await Router.GoToAsync("/form", cancellationToken: TestContext.Current.CancellationToken);
            return (FormVm)Router.CurrentViewModel!;
        }

        public static async Task Until(Func<bool> condition)
        {
            for (var i = 0; i < 300 && !condition(); i++)
                await Task.Delay(10);
            Assert.True(condition(), "condition not reached");
        }

        public async ValueTask DisposeAsync()
        {
            await Sync.DisposeAsync();
            await Provider.DisposeAsync();
        }
    }

    // ---- prerendering

    [Fact]
    public async Task A_router_that_throws_while_the_page_is_prerendered_gives_false_and_the_error_is_reported()
    {
        var errors = new Errors();
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<FakeViewFor, FakeViewModel>(route: "/home");
        services.AddSingleton<IRouteableErrorSink>(errors);
        var navigation = new FakeNavigation();
        services.AddSingleton<NavigationManager>(navigation);
        await using var provider = services.BuildServiceProvider();
        var router = Substitute.For<IRouter>();
        router.GoToAsync(Arg.Any<string>(), Arg.Any<IRouteableArgs?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<RouteableResult>(new InvalidOperationException("the page cannot be built")));
        var sync = new RouteableUrlSync(navigation, router, provider);

        var shown = await sync.PrerenderAsync();

        Assert.False(shown);
        Assert.Equal("the page cannot be built", Assert.Single(errors.Reported).Message);
    }

    // ---- the warning before the tab closes

    [Fact]
    public async Task Without_a_script_runtime_a_page_with_unsaved_changes_asks_nothing_of_the_browser()
    {
        await using var f = new Fixture(scripts: null, form: true);
        await f.Sync.StartAsync();
        var form = await f.ShowFormAsync();

        form.Edit(true);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.True(form.HasUnsavedChanges);
        Assert.Empty(f.Errors.Reported);
    }

    [Fact]
    public async Task A_script_that_cannot_be_loaded_is_tried_again_at_the_next_change()
    {
        var scripts = new Scripts { FailFirst = 1 };
        await using var f = new Fixture(scripts, form: true);
        await f.Sync.StartAsync();
        var form = await f.ShowFormAsync();

        form.Edit(true);
        await Fixture.Until(() => scripts.Imports == 1);
        Assert.Empty(scripts.Warn);                       // prerendering or no script: no tab to warn

        form.Edit(true);
        await Fixture.Until(() => scripts.Warn.Count == 1);
        Assert.True(scripts.Warn[0]);
        Assert.Equal(2, scripts.Imports);
    }

    [Fact]
    public async Task A_script_that_arrives_after_the_sync_ended_is_not_used_and_is_let_go_of()
    {
        var scripts = new Scripts { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var f = new Fixture(scripts, form: true);
        await f.Sync.StartAsync();
        var form = await f.ShowFormAsync();

        form.Edit(true);
        await Fixture.Until(() => scripts.Imports == 1);
        f.Sync.Dispose();                                 // the circuit ends while the script is loading
        scripts.Gate.SetResult();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        await f.Sync.DisposeAsync();

        Assert.Empty(scripts.Warn);
        Assert.Equal(1, scripts.Disposed);
    }

    [Fact]
    public async Task A_script_that_fails_because_the_circuit_is_gone_does_not_make_the_disposal_fail()
    {
        var scripts = new Scripts { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var f = new Fixture(scripts, form: true);
        await f.Sync.StartAsync();
        var form = await f.ShowFormAsync();
        form.Edit(true);
        await Fixture.Until(() => scripts.Imports == 1);

        var disposing = f.Sync.DisposeAsync().AsTask();
        scripts.Gate.SetException(new JSDisconnectedException("the circuit is gone"));

        await disposing;
        Assert.Empty(scripts.Warn);
    }

    [Fact]
    public async Task After_the_sync_is_disposed_the_page_it_watched_no_longer_speaks_for_the_tab()
    {
        var scripts = new Scripts();
        await using var f = new Fixture(scripts, form: true);
        await f.Sync.StartAsync();
        var form = await f.ShowFormAsync();

        f.Sync.Dispose();
        form.Edit(true);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(0, scripts.Imports);
    }

    // ---- the addresses the browser asks about

    [Fact]
    public async Task An_address_of_another_site_is_not_for_the_router_and_is_let_through()
    {
        await using var f = new Fixture();
        await f.Sync.StartAsync();

        var allowed = await f.Navigation.AskAsync("https://elsewhere.example/page", intercepted: true);

        Assert.True(allowed);
        Assert.Empty(f.Navigation.Calls);
    }

    [Fact]
    public async Task The_address_of_the_page_that_is_shown_is_let_through_without_routing()
    {
        await using var f = new Fixture();
        await f.Sync.StartAsync();

        Assert.True(await f.Navigation.AskAsync("http://localhost/home"));
        Assert.Empty(f.Navigation.Calls);
    }

    [Fact]
    public async Task A_menu_that_fails_while_the_address_is_asked_about_cancels_the_navigation_and_reports()
    {
        await using var f = new Fixture(withMenu: true);
        await f.Sync.StartAsync();
        f.Menu.Select = () => throw new InvalidOperationException("the menu failed");

        var allowed = await f.Navigation.AskAsync("http://localhost/about", intercepted: true);

        Assert.False(allowed);
        Assert.Equal("the menu failed", Assert.Single(f.Errors.Reported).Message);
    }

    [Fact]
    public async Task A_menu_that_refuses_while_the_address_is_asked_about_cancels_the_navigation_and_changes_no_address()
    {
        await using var f = new Fixture(withMenu: true);
        await f.Sync.StartAsync();
        f.Menu.Select = () => Task.FromResult(false);

        var allowed = await f.Navigation.AskAsync("http://localhost/about", intercepted: true);

        Assert.False(allowed);
        Assert.Empty(f.Navigation.Calls);                 // the browser stays where it was: Blazor cancels the move itself
        Assert.Empty(f.Errors.Reported);
    }

    // ---- the address changed

    [Fact]
    public async Task A_menu_that_refuses_an_address_the_browser_already_changed_puts_the_address_back()
    {
        await using var f = new Fixture(withMenu: true);
        await f.Sync.StartAsync();
        f.Menu.Select = () => Task.FromResult(false);

        f.Navigation.BrowserGoesTo("/about");

        await Fixture.Until(() => f.Navigation.Calls.Count == 1);
        Assert.Equal(("/home", true), f.Navigation.Calls[0]);
    }

    [Fact]
    public async Task A_menu_that_fails_for_an_address_the_browser_already_changed_reports_and_puts_the_address_back()
    {
        await using var f = new Fixture(withMenu: true);
        await f.Sync.StartAsync();
        f.Menu.Select = () => throw new InvalidOperationException("the menu failed");

        f.Navigation.BrowserGoesTo("/about");

        await Fixture.Until(() => f.Navigation.Calls.Count == 1 && f.Errors.Reported.Count >= 1);
        Assert.Equal(("/home", true), f.Navigation.Calls[0]);
        Assert.Equal("the menu failed", f.Errors.Reported[0].Message);
    }

    [Fact]
    public async Task An_address_that_cannot_be_put_back_while_prerendering_is_reported_and_not_thrown()
    {
        await using var f = new Fixture(withMenu: true);
        await f.Sync.StartAsync();
        f.Menu.Select = () => Task.FromResult(false);
        f.Navigation.ThrowOnNavigate = true;

        f.Navigation.BrowserGoesTo("/about");

        await Fixture.Until(() => f.Errors.Reported.Count >= 1);
        Assert.Equal("the page is only being prerendered", f.Errors.Reported[0].Message);
    }

    private static readonly (string Template, Type? ViewModelType)[] Items = [("/items/{id}", null), ("/orders", null)];

    [Fact]
    public void The_case_of_a_value_makes_another_page_and_the_case_of_a_word_does_not()
    {
        Assert.False(RouteableUrlSync.Same("/items/aB", "/items/Ab", Items));      // a token, a slug: two pages
        Assert.True(RouteableUrlSync.Same("/Orders", "/orders", Items));           // a word of the route
        Assert.True(RouteableUrlSync.Same("/ITEMS/aB", "/items/aB", Items));
        Assert.True(RouteableUrlSync.Same("/items/aB", "/items/aB", Items));
        Assert.True(RouteableUrlSync.Same("/Whatever", "/whatever"));              // no route is known: nothing to protect
    }
}
