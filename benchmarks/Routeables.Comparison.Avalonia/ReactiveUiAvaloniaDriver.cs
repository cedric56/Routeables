using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Unit = System.Reactive.Unit;

namespace Routeables.Comparison.Avalonia;

/// <summary>A routable view model: the name it gives itself, and the screen that hosts it.</summary>
public sealed class RxPage(string name, IScreen host) : ReactiveObject, IRoutableViewModel
{
    public string Name { get; } = name;

    public string? UrlPathSegment => Name;

    public IScreen HostScreen { get; } = host;

    /// <summary>ReactiveUI routing has no "can I leave": the application asks the page before it navigates.</summary>
    public Func<bool> CanLeave { get; set; } = static () => true;
}

/// <summary>The view of a page: a control with a text, built with the page it shows.</summary>
public sealed class RxView : UserControl
{
    public RxView(RxPage page)
    {
        DataContext = page;
        Content = new TextBlock { Text = page.Name };
    }
}

public sealed class RxScreen : ReactiveObject, IScreen
{
    // The immediate scheduler: the application's own thread is the UI thread, already the one the commands run on.
    public RoutingState Router { get; } = new(ImmediateScheduler.Instance);
}

/// <summary>
/// The scenario on <c>RoutingState</c> of ReactiveUI, shown by a view host written here: the host shows the view of the page the router
/// gives (<c>RoutedViewHost</c> of ReactiveUI.Avalonia does the same, with its view locator; its API could not be checked, so it is not used). The host is a
/// <c>TransitioningContentControl</c>, the control that <c>RouteableRegion</c> is, so that the two libraries pay the same for the control.
/// The library has a navigation stack, not a history (no Forward), no route table and no guard: the guard and the refusal to leave are
/// written in the application, in front of <c>Navigate</c>. Pages and views are built through the container, as Routeables builds its own.
/// </summary>
public sealed class ReactiveUiAvaloniaDriver : INavigationDriver
{
    private readonly RxScreen _screen = new();
    private readonly ServiceProvider _provider = new ServiceCollection().AddSingleton<IClock, Clock>().BuildServiceProvider();
    private readonly Window _window;
    private readonly IDisposable _host;

    public ReactiveUiAvaloniaDriver()
    {
        State = new AppState();
        // The control of the region of Routeables (RouteableRegion is a TransitioningContentControl): two presenters and the machinery of a
        // transition, with none declared. A plain ContentControl would leave that cost to Routeables alone.
        var content = new TransitioningContentControl();
        _host = _screen.Router.CurrentViewModel.Subscribe(
            vm => content.Content = vm is RxPage page ? ActivatorUtilities.CreateInstance<RxView>(_provider, page) : null);
        _window = new Window { Width = 400, Height = 300, Content = content };
        _window.Show();
        Flush();
    }

    public string Name => "ReactiveUI routing";

    public AppState State { get; }

    public bool SupportsForward => false;

    public string? Current => (_screen.Router.NavigationStack.LastOrDefault() as RxPage)?.Name;

    public object? CurrentObject => _screen.Router.NavigationStack.LastOrDefault();

    public async Task GoAsync(string target)
    {
        if (!CanLeave())
            return;
        if (AppState.IsGuarded(target) && !State.SignedIn)
        {
            State.Requested = target;
            target = "SignIn";
        }

        await _screen.Router.Navigate.Execute(Create(target.Split('/')));
        Flush();
    }

    public async Task BackAsync()
    {
        if (_screen.Router.NavigationStack.Count > 1 && CanLeave())
            await _screen.Router.NavigateBack.Execute(Unit.Default);
        Flush();
    }

    public Task ForwardAsync() => throw new NotSupportedException("ReactiveUI routing has no Forward.");

    public Task<bool> ClearHistoryAsync()
    {
        var current = _screen.Router.NavigationStack.LastOrDefault();
        _screen.Router.NavigationStack.Clear();
        if (current is not null)
            _screen.Router.NavigationStack.Add(current);
        Flush();
        return Task.FromResult(true);
    }

    private bool CanLeave() => _screen.Router.NavigationStack.LastOrDefault() is not RxPage page || page.CanLeave();

    private RxPage Create(string[] parts) => parts[0] switch
    {
        ['C', >= '0' and <= '9', ..] when parts.Length > 1 => Page($"{parts[0]}:{parts[1]}"),
        "Detail" => Page($"Detail:{parts[1]}"),
        "Edit" => Page($"Edit:{parts[1]}", () => !State.Dirty),
        ['P', '3', '0' or '1' or '2' or '3' or '4'] => PageWithDependency(parts[0]),
        _ => Page(parts[0]),
    };

    private RxPage PageWithDependency(string name)
    {
        _ = _provider.GetRequiredService<IClock>().Now;   // the dependency of the five pages that have one
        return Page(name);
    }

    private RxPage Page(string name, Func<bool>? canLeave = null)
    {
        var page = ActivatorUtilities.CreateInstance<RxPage>(_provider, name, _screen);
        if (canLeave is not null)
            page.CanLeave = canLeave;
        return page;
    }

    private static void Flush() => Dispatcher.UIThread.RunJobs();

    public void Dispose()
    {
        _host.Dispose();
        _window.Close();
        Flush();
        _provider.Dispose();
    }
}
