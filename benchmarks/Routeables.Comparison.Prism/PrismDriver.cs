using Avalonia.Threading;
using Routeables.Comparison.Avalonia;
using Prism.Ioc;
using Prism.Navigation.Regions;

namespace Routeables.Comparison.Prism;

/// <summary>
/// The scenario on the region navigation of Prism.Avalonia: <c>RequestNavigate</c> by the name a page was registered with, a journal with
/// Back and Forward, and the page that refuses to be left (<c>IConfirmNavigationRequest</c>). Prism has no guard: it is written in the
/// application, in front of <c>RequestNavigate</c>.
/// Two configurations are measured: the pages say <c>KeepAlive = false</c> (<c>IRegionMemberLifetime</c>), so that the region drops the views
/// that are no longer active, or they do not (the default of Prism, <see cref="PrismBenchApp.KeepViews"/>) and the region keeps them.
/// When the history is cleared the driver removes the views that are left, so that both end in the same state; the count of the views alive
/// after the clear says whether that was needed.
/// </summary>
public sealed class PrismDriver : INavigationDriver
{
    private readonly IRegion _region;

    public PrismDriver()
    {
        var services = PrismBenchApp.Services;
        ViewCount.Owner = Name;
        State = services.Resolve<AppState>();
        State.SignedIn = false;
        State.Dirty = false;
        State.Requested = null;
        _region = services.Resolve<IRegionManager>().Regions[PrismBenchApp.Region];
        Reset();
        PrismBenchApp.Shell.Show();
        Flush();
    }

    public string Name => PrismBenchApp.KeepViews ? "Prism (views kept)" : "Prism (KeepAlive = false)";

    public AppState State { get; }

    public bool SupportsForward => true;

    // The journal knows the address that was asked for: "C3?id=7" is the page "C3:7".
    public string? Current => _region.NavigationService.Journal.CurrentEntry?.Uri?.OriginalString is { } uri
        ? AppState.NameOf(uri.Replace("?id=", "/"))
        : null;

    public object? CurrentObject => _region.ActiveViews.FirstOrDefault();

    public async Task GoAsync(string target)
    {
        if (AppState.IsGuarded(target) && !State.SignedIn)
        {
            State.Requested = target;
            target = "SignIn";
        }

        var parts = target.Split('/');
        var uri = parts.Length > 1 ? $"{parts[0]}?id={parts[1]}" : parts[0];
        var journal = _region.NavigationService.Journal;
        var before = journal.CurrentEntry;
        var done = new TaskCompletionSource();
        _region.NavigationService.RequestNavigate(new Uri(uri, UriKind.Relative), _ => done.TrySetResult());
        await done.Task;
        Flush();
        var navigated = !ReferenceEquals(before, journal.CurrentEntry);

        // The journal says where the navigation went. The view must also be that page's, or the numbers would not be those of the page shown
        // (Prism finds a page's view by its type: see PrismPages.cs).
        if (navigated)
        {
            var expected = parts[0] == "Edit" ? nameof(PrismEditPage) : parts[0] + "Page";
            var shown = PrismBenchApp.Host.Content?.GetType().Name;
            if (shown != expected)
                throw new InvalidOperationException($"Prism navigated to '{target}' and the region shows {shown ?? "no view"}, expected {expected}.");
        }
    }

    public Task BackAsync()
    {
        var journal = _region.NavigationService.Journal;
        if (journal.CanGoBack)
            journal.GoBack();
        Flush();
        return Task.CompletedTask;
    }

    public Task ForwardAsync()
    {
        var journal = _region.NavigationService.Journal;
        if (journal.CanGoForward)
            journal.GoForward();
        Flush();
        return Task.CompletedTask;
    }

    public Task<bool> ClearHistoryAsync()
    {
        ViewCount.Note($"Prism: the region holds {_region.Views.Count()} views before the history is cleared.");
        ViewCount.NoteAlive("before the history is cleared");

        _region.NavigationService.Journal.Clear();

        // The views that are left (all of them when the region keeps its views) are removed, except the current one.
        var current = _region.ActiveViews.FirstOrDefault();
        foreach (var view in _region.Views.Where(v => !ReferenceEquals(v, current)).ToList())
            _region.Remove(view);

        Flush();
        ViewCount.Note($"Prism: the region holds {_region.Views.Count()} views after the history is cleared.");
        ViewCount.NoteAlive("after the history is cleared");
        return Task.FromResult(true);
    }

    private void Reset()
    {
        foreach (var view in _region.Views.ToList())
            _region.Remove(view);
        _region.NavigationService.Journal.Clear();
    }

    private void Flush()
    {
        Dispatcher.UIThread.RunJobs();
        LayoutCount.Observe(Name, PrismBenchApp.Host?.Content);
    }

    public void Dispose() => Reset();
}