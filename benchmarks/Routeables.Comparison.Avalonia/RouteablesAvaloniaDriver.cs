using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction.Routing;
using Routeables.Avalonia;

namespace Routeables.Comparison.Avalonia;

/// <summary>The scenario on the router of Routeables, shown by a <see cref="RouteableRegion"/> in a window.</summary>
public sealed class RouteablesAvaloniaDriver : INavigationDriver
{
    private readonly IRouter _router;
    // One window and one region for the whole run: the router of Routeables is the application's, and a region closed with the window of the
    // previous driver is not one a new window can take over.
    private static Window? s_window;

    private bool _started;

    public RouteablesAvaloniaDriver()
    {
        var provider = BenchApp.Provider;
        State = provider.GetRequiredService<AppState>();
        State.SignedIn = false;
        State.Dirty = false;
        State.Requested = null;
        _router = provider.GetRouter(null);
        if (s_window is null)
        {
            s_window = new Window { Width = 400, Height = 300, Content = new RouteableRegion() };
            s_window.Show();
        }

        Flush();
    }

    public string Name => BenchApp.HistoryLimit is { } limit ? $"Routeables (history limited to {limit})" : "Routeables";

    public AppState State { get; }

    public bool SupportsForward => true;

    // Until the first step of this run the router still holds the page of the previous run.
    public string? Current => _started ? (_router.CurrentViewModel as RVm)?.Name : null;

    public object? CurrentObject => _router.CurrentViewModel;

    public async Task GoAsync(string target)
    {
        await StartAsync();
        await _router.GoToAsync("/" + target.ToLowerInvariant());
        Flush();
    }

    public async Task BackAsync()
    {
        await StartAsync();
        await _router.GoBackAsync();
        Flush();
    }

    public async Task ForwardAsync()
    {
        await StartAsync();
        await _router.ForwardAsync();
        Flush();
    }

    public async Task<bool> ClearHistoryAsync()
    {
        // What the memory measure has just weighed: the entries that the history still keeps (each one has its view model, its view and its arguments).
        Console.Error.WriteLine($"::notice title=History::{Name}: {_router.History.Count} entries kept before the history is cleared");
        await _router.EraseHistoryAsync();
        Flush();
        return true;
    }

    private async Task StartAsync()
    {
        if (_started)
            return;
        _started = true;
        await _router.EraseHistoryAsync();
    }

    /// <summary>Runs what the application has queued after a step: the binding of the view, the layout of the window.</summary>
    private void Flush()
    {
        Dispatcher.UIThread.RunJobs();
        LayoutCount.Observe(Name, (s_window?.Content as RouteableRegion)?.Content);
    }

    public void Dispose() => Flush();
}
