using Avalonia.Threading;
using Prism.Ioc;
using Prism.Navigation.Regions;

namespace Routeables.Comparison.Prism;

/// <summary>
/// The scenario on the region navigation of Prism.Avalonia: <c>RequestNavigate</c> by the name a page was registered with, a journal with
/// Back and Forward, and the page that refuses to be left (<c>IConfirmNavigationRequest</c>). Prism has no guard: it is written in the
/// application, in front of <c>RequestNavigate</c>. Prism keeps the views it has shown in the region: clearing the history also removes them,
/// which is what an application has to do.
/// </summary>
public sealed class PrismDriver : INavigationDriver
{
    private readonly IRegion _region;

    public PrismDriver()
    {
        var services = PrismBenchApp.Services;
        State = services.Resolve<AppState>();
        State.SignedIn = false;
        State.Dirty = false;
        State.Requested = null;
        _region = services.Resolve<IRegionManager>().Regions[PrismBenchApp.Region];
        Reset();
        PrismBenchApp.Shell.Show();
        Flush();
    }

    public string Name => "Prism";

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
        var done = new TaskCompletionSource();
        _region.NavigationService.RequestNavigate(new Uri(uri, UriKind.Relative), _ => done.TrySetResult());
        await done.Task;
        Flush();
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
        var current = _region.ActiveViews.FirstOrDefault();
        foreach (var view in _region.Views.Where(v => !ReferenceEquals(v, current)).ToList())
            _region.Remove(view);
        _region.NavigationService.Journal.Clear();
        Flush();
        return Task.FromResult(true);
    }

    private void Reset()
    {
        foreach (var view in _region.Views.ToList())
            _region.Remove(view);
        _region.NavigationService.Journal.Clear();
    }

    private static void Flush() => Dispatcher.UIThread.RunJobs();

    public void Dispose() => Reset();
}
