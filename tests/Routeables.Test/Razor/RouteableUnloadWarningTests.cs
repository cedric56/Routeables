using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Blazor;
using Routeables.Routing;

namespace Routeables.Tests.Razor;

/// <summary>While the page shown has unsaved changes (<see cref="IRouteableUnsavedChanges"/>), the browser is asked to confirm before the tab closes.</summary>
public class RouteableUnloadWarningTests
{
    private sealed class FakeNavigation : NavigationManager
    {
        public FakeNavigation() => Initialize("http://localhost/", "http://localhost/home");
        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            Uri = ToAbsoluteUri(uri).AbsoluteUri;
            NotifyLocationChanged(isInterceptedLink: false);
        }
    }

    private sealed class Scripts : IJSRuntime
    {
        public List<bool> Warn { get; } = [];
        public int Disposed { get; set; }
        public int Imports { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier != "import") return default;
            Imports++;
            return new((TValue)(object)new Module(this));
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);

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

    public sealed class FormView : IRouteableFor { public object? Context { get; set; } }
    public sealed class FormVm : IRouteableUnsavedChanges
    {
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public event EventHandler? UnsavedChangesChanged;
        public void Edit(bool dirty) { _dirty = dirty; UnsavedChangesChanged?.Invoke(this, EventArgs.Empty); }
    }

    public sealed class PlainView : IRouteableFor { public object? Context { get; set; } }
    public sealed class PlainVm;

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
            await Task.Delay(10);
        Assert.True(condition(), "condition not reached");
    }

    [Fact]
    public async Task The_browser_is_told_when_the_page_gets_and_loses_unsaved_changes_and_when_the_page_is_left()
    {
        var scripts = new Scripts();
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<FormView, FormVm>(route: "/form");
        services.RegisterRoute<PlainView, PlainVm>(route: "/plain");
        services.AddSingleton<NavigationManager>(new FakeNavigation());
        services.AddSingleton<IJSRuntime>(scripts);
        await using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();
        var sync = new RouteableUrlSync(provider.GetRequiredService<NavigationManager>(), router, provider);
        await sync.StartAsync();
        var ct = TestContext.Current.CancellationToken;

        await router.GoToAsync("/plain", cancellationToken: ct);
        Assert.Equal(0, scripts.Imports);                            // no page with unsaved changes yet: the script is not even loaded

        await router.GoToAsync("/form", cancellationToken: ct);
        var form = (FormVm)router.CurrentViewModel!;
        await Task.Delay(50);
        Assert.Equal(0, scripts.Imports);                            // a page with nothing to lose does not load the script either

        form.Edit(true);
        await Until(() => scripts.Warn.Count == 1);
        Assert.True(scripts.Warn[^1]);

        await router.GoToAsync("/plain", cancellationToken: ct);       // leaving the page ends the warning
        await Until(() => scripts.Warn.Count == 2);
        Assert.False(scripts.Warn[^1]);

        form.Edit(false);                                              // an old page no longer speaks for the tab
        await Task.Delay(50);
        Assert.Equal(2, scripts.Warn.Count);

        await sync.DisposeAsync();
        Assert.Equal(1, scripts.Disposed);                             // the listener of the tab does not outlive the circuit
    }
}
