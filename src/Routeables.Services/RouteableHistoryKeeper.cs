using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Abstraction;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.Routing;

namespace Routeables.Services;

/// <summary>Where the history of the router is kept between two runs of the application.</summary>
public interface IRouteableHistoryStore
{
    /// <summary>What <see cref="Save"/> kept last, or <see langword="null"/> when nothing was kept (first run, unreadable file).</summary>
    string? Load();

    /// <summary>Keeps the history, replacing what was kept.</summary>
    /// <param name="text">The history as <see cref="RouteableState.Serialize(IEnumerable{RouteableSnapshot})"/> wrote it.</param>
    void Save(string text);
}

/// <summary>
/// <see cref="IRouteableHistoryStore"/> in a text file. Without a path the file is <c>routeables-history.txt</c> in a folder named after the
/// application, in the local application data of the user (the sandbox of the application on MAUI).
/// </summary>
public sealed class FileRouteableHistoryStore : IRouteableHistoryStore
{
    /// <summary>Creates the store.</summary>
    /// <param name="path">The file; <see langword="null"/> takes the default one.</param>
    public FileRouteableHistoryStore(string? path = null) => Path = path ?? DefaultPath();

    /// <summary>The file.</summary>
    public string Path { get; }

    /// <inheritdoc />
    public string? Load()
    {
        try { return File.Exists(Path) ? File.ReadAllText(Path) : null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <inheritdoc />
    public void Save(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        // Written aside and moved over the old file: a run that stops in the middle of a write keeps the previous history.
        var temporary = Path + ".tmp";
        File.WriteAllText(temporary, text);
        File.Move(temporary, Path, overwrite: true);
    }

    private static string DefaultPath()
    {
        var name = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "Routeables";
        return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), name, "routeables-history.txt");
    }
}

/// <summary>
/// Keeps the history of the router between two runs of the application, on every host that has a container of its own (WPF, Avalonia, WinUI,
/// Uno, WinUI 3, MAUI): the pages shown, with what they saved (<see cref="RouteableState"/>), are written to an
/// <see cref="IRouteableHistoryStore"/> after each route change and put back when the application starts again, once its first page is shown.
/// Switched on by <see cref="RouteableHistoryExtensions.AddRouteableHistory"/>; the host starts it when it builds its container.
/// </summary>
/// <remarks>
/// Blazor keeps the history of a circuit with <c>PersistentComponentState</c> instead (see <c>RouteableHistoryOptions</c>), and the ASP.NET Core
/// host keeps a scope per visitor in memory.
/// </remarks>
public sealed class RouteableHistoryKeeper(IServiceProvider provider, IRouteableHistoryStore store) : IDisposable
{
    private readonly object _gate = new();
    private IRouter? _router;
    private volatile bool _active;
    private string? _last;

    /// <summary>Starts keeping the history: reads what was kept, and writes the history after each route change from then on. Safe to call twice.</summary>
    public void Start()
    {
        IRouter? router;
        lock (_gate)
        {
            if (_router is not null || provider.GetService<IRouter>() is not { } found)
                return;
            _router = router = found;
        }

        IReadOnlyList<RouteableSnapshot> kept = [];
        try { kept = RouteableState.DeserializeSnapshots(store.Load()); }
        catch (Exception ex) { RouteableErrors.Report<RouteableHistoryKeeper>(provider, ex, "Load"); }

        router.Routed += OnRouted;
        router.History.Changed += OnHistoryChanged;

        // Nothing is written before the pages are back: the first page of the new run must not replace the history that is waiting.
        if (kept.Count == 0)
        {
            _active = true;
            return;
        }

        RouteableHelper.FireAndForget(() => Task.Run(() => RestoreAsync(router, kept)), provider, "RouteableHistoryKeeper.Restore");
    }

    private async Task RestoreAsync(IRouter router, IReadOnlyList<RouteableSnapshot> kept)
    {
        try
        {
            if (!await RouteableLinkInbox.WaitForFirstPageAsync(router))
                return;

            async Task Restore()
            {
                if (await RouteableState.RestoreSnapshotsAsync(router, kept))
                    RouteableMenu.Moved(provider, router);
            }

            if (provider.GetService<IRouteableDispatcher>() is { } dispatcher && !dispatcher.CheckAccess())
                await dispatcher.InvokeAsync(Restore);
            else
                await Restore();
        }
        catch (Exception ex)
        {
            RouteableErrors.Report<RouteableHistoryKeeper>(provider, ex, "Restore");
        }
        finally
        {
            _active = true;
            try
            {
                // The history belongs to the thread of the interface: read it there, as the route changes that save it do.
                if (provider.GetService<IRouteableDispatcher>() is { } dispatcher && !dispatcher.CheckAccess())
                    await dispatcher.InvokeAsync(SaveNow);
                else
                    SaveNow();
            }
            catch (Exception ex)
            {
                RouteableErrors.Report<RouteableHistoryKeeper>(provider, ex, "Save");
            }
        }
    }

    private void OnRouted(object? sender, RouteableRoutedEventArgs e)
    {
        if (_active && e.Succeeded)
            SaveNow();
    }

    // An erased history raises no route change: without this, the pages that were erased would come back at the next start.
    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        if (_active && _router is { } router && router.History.Count == 0)
            SaveNow(allowEmpty: true);
    }

    /// <summary>Writes the history now, without waiting for the next route change (the application is going to sleep). Does nothing before <see cref="Start"/> has put the previous history back.</summary>
    public void Flush()
    {
        if (_active) SaveNow();
    }

    private void SaveNow() => SaveNow(allowEmpty: false);

    private void SaveNow(bool allowEmpty)
    {
        try
        {
            if (_router is not { } router)
                return;

            var snapshots = RouteableState.CaptureSnapshots(router);
            // An empty history is written only when it was erased: the first page of a new run is not yet there when the keeper starts.
            if (snapshots.Count == 0 && !allowEmpty)
                return;

            var text = snapshots.Count == 0 ? string.Empty : RouteableState.Serialize(snapshots);
            lock (_gate)
            {
                if (text == _last)
                    return;
                store.Save(text);
                _last = text;
            }
        }
        catch (Exception ex)
        {
            // A history that cannot be written (a read-only disk) must not fail a route change.
            RouteableErrors.Report<RouteableHistoryKeeper>(provider, ex, "Save");
        }
    }

    /// <summary>Stops keeping the history.</summary>
    public void Dispose()
    {
        _active = false;
        if (_router is { } router)
            router.Routed -= OnRouted;
        if (_router is { } erased)
            erased.History.Changed -= OnHistoryChanged;
    }
}

/// <summary>Registration of <see cref="RouteableHistoryKeeper"/>.</summary>
public static class RouteableHistoryExtensions
{
    /// <summary>
    /// Keeps the history of the router between two runs of the application (see <see cref="RouteableHistoryKeeper"/>). Call it with the other
    /// registrations of the application; the host starts it when it builds its container.
    /// </summary>
    /// <param name="services">The service collection of the application.</param>
    /// <param name="path">The file that holds the history; <see langword="null"/> takes <see cref="FileRouteableHistoryStore"/>'s default.</param>
    public static IServiceCollection AddRouteableHistory(this IServiceCollection services, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IRouteableHistoryStore>(_ => new FileRouteableHistoryStore(path));
        services.TryAddSingleton<RouteableHistoryKeeper>();
        return services;
    }

    /// <summary>Starts the keeper when <see cref="AddRouteableHistory"/> registered it; does nothing otherwise. A host calls it once its container is built.</summary>
    public static IServiceProvider StartRouteableHistory(this IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        provider.GetService<RouteableHistoryKeeper>()?.Start();
        return provider;
    }
}
