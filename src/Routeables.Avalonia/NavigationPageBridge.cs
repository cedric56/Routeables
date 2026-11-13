using Avalonia.Controls;
using Avalonia.Threading;
using Routeables.Abstraction;
using Routeables.Abstraction.Errors;
using Routeables.Abstraction.History;
using Routeables.Abstraction.Routing;
using Routeables.Regions;

namespace Routeables.Avalonia;

/// <summary>
/// Keeps the pile of pages of a <see cref="NavigationPage"/> equal to the history of a router (the entries from the first to the current one).
/// The router decides; the control shows. What the user does on the control (back button, swipe) is turned into a request to the router.
/// </summary>
internal sealed class NavigationPageBridge
{
    private readonly NavigationPage _navigation;
    private readonly IServiceProvider _provider;
    private readonly IRouter _router;
    private readonly Func<IHistoryEntry, object?> _header;

    // One page per entry, for as long as the entry is in the history (Forward shows the same page again).
    private readonly Dictionary<IHistoryEntry, Page> _pages = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Page, IHistoryEntry> _entries = new(ReferenceEqualityComparer.Instance);

    private bool _applying;     // the bridge itself is popping: the control may do it
    private bool _dirty;
    private bool _running;
    private bool _disposed;

    public NavigationPageBridge(NavigationPage navigation, IServiceProvider provider, string? region, Func<IHistoryEntry, object?>? header)
    {
        _navigation = navigation;
        _provider = provider;
        _router = provider.GetRouter(region);
        _header = header ?? DefaultHeader;
    }

    public void Start()
    {
        _router.History.Changed += OnHistoryChanged;
        Request();
    }

    public void Dispose()
    {
        _disposed = true;
        _router.History.Changed -= OnHistoryChanged;
        foreach (var page in _entries.Keys)
            page.Navigating -= OnNavigating;
        _pages.Clear();
        _entries.Clear();
    }

    // The history changes on the thread of the route change, which is not always the one of the interface.
    private void OnHistoryChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(Request);

    private void Request()
    {
        if (_disposed) return;
        _dirty = true;
        if (_running) return;
        _running = true;
        _ = PumpAsync();
    }

    // One pass at a time: the control ignores a navigation asked while another one runs (IsNavigating).
    private async Task PumpAsync()
    {
        try
        {
            while (_dirty && !_disposed)
            {
                _dirty = false;
                await ApplyAsync();
            }
        }
        catch (Exception ex)
        {
            RouteableErrors.Report(_provider, ex, typeof(RouteablePages));
        }
        finally
        {
            _running = false;
        }
    }

    private async Task ApplyAsync()
    {
        var history = _router.History;
        var entries = history.Entries;
        var wanted = history.CurrentIndex < 0 ? new List<IHistoryEntry>() : entries.Take(history.CurrentIndex + 1).ToList();
        Forget(entries);

        var actual = _navigation.NavigationStack
            .Select(page => _entries.TryGetValue(page, out var entry) ? (object)entry : page)
            .ToList();

        foreach (var step in PageStackPlanner.Plan(actual, wanted.Cast<object>().ToList()))
        {
            if (_disposed) return;
            await WaitUntilIdleAsync();
            _applying = true;
            try
            {
                switch (step.Kind)
                {
                    case PageStackStepKind.PopTo:
                        var stack = _navigation.NavigationStack;
                        if (stack.Count - step.Depth == 1)
                            await _navigation.PopAsync();
                        else
                            await _navigation.PopToPageAsync(stack[step.Depth - 1]);
                        break;
                    case PageStackStepKind.Push:
                        await _navigation.PushAsync(PageOf((IHistoryEntry)step.Entry!));
                        break;
                    case PageStackStepKind.Replace:
                        await _navigation.ReplaceAsync(PageOf((IHistoryEntry)step.Entry!));
                        break;
                }
            }
            finally
            {
                _applying = false;
            }
        }
    }

    // A control ignores a navigation asked during a transition: waits for the end of the one that runs.
    private async Task WaitUntilIdleAsync()
    {
        while (_navigation.IsNavigating && !_disposed)
        {
            var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnChanged(object? sender, global::Avalonia.AvaloniaPropertyChangedEventArgs e)
            {
                if (e.Property == NavigationPage.IsNavigatingProperty)
                    idle.TrySetResult();
            }

            _navigation.PropertyChanged += OnChanged;
            try
            {
                if (_navigation.IsNavigating)
                    await idle.Task;
            }
            finally
            {
                _navigation.PropertyChanged -= OnChanged;
            }
        }
    }

    private Page PageOf(IHistoryEntry entry)
    {
        if (_pages.TryGetValue(entry, out var known))
            return known;

        var page = entry.View as Page ?? new ContentPage { Content = entry.View, Header = _header(entry) };
        page.Navigating += OnNavigating;
        _pages[entry] = page;
        _entries[page] = entry;
        return page;
    }

    // The pages of the entries that left the history are not kept.
    private void Forget(IReadOnlyList<IHistoryEntry> inHistory)
    {
        if (_pages.Count <= inHistory.Count) return;
        var alive = new HashSet<IHistoryEntry>(inHistory, ReferenceEqualityComparer.Instance);
        foreach (var gone in _pages.Keys.Where(entry => !alive.Contains(entry)).ToList())
        {
            var page = _pages[gone];
            page.Navigating -= OnNavigating;
            _pages.Remove(gone);
            _entries.Remove(page);
        }
    }

    // The user pops the page (back button, swipe): the router answers, not the control. The pop is cancelled here; when the router agrees, its history
    // changes and the bridge pops the page itself. Not awaited: the control waits for this handler, and the bridge waits for the control.
    private Task OnNavigating(NavigatingFromEventArgs args)
    {
        if (_applying || args.NavigationType != NavigationType.Pop)
            return Task.CompletedTask;

        args.Cancel = true;
        Dispatcher.UIThread.Post(async () =>
        {
            try { await _router.GoBackAsync(); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { RouteableErrors.Report(_provider, ex, typeof(RouteablePages)); }
        });
        return Task.CompletedTask;
    }

    // "DogDetailsViewModel" -> "Dog Details".
    private static object? DefaultHeader(IHistoryEntry entry)
    {
        var name = entry.ViewModel.GetType().Name;
        if (name.EndsWith("ViewModel", StringComparison.Ordinal) && name.Length > "ViewModel".Length)
            name = name[..^"ViewModel".Length];
        return string.Join(' ', name.SplitOnCapitals());
    }
}
