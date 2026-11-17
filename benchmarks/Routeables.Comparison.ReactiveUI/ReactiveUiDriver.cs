using ReactiveUI;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Unit = System.Reactive.Unit;

namespace Routeables.Comparison;

/// <summary>A routable view model: the name it gives itself, and the screen that hosts it.</summary>
public sealed class RxPage(string name, IScreen host) : ReactiveObject, IRoutableViewModel
{
    public string Name { get; } = name;

    public string? UrlPathSegment => Name;

    public IScreen HostScreen { get; } = host;

    /// <summary>ReactiveUI routing has no "can I leave": the application asks the page before it navigates.</summary>
    public Func<bool> CanLeave { get; init; } = static () => true;
}

public sealed class RxScreen : ReactiveObject, IScreen
{
    // The immediate scheduler: the console has no UI thread to hand the commands to.
    public RoutingState Router { get; } = new(ImmediateScheduler.Instance);
}

/// <summary>
/// The scenario on <c>RoutingState</c> of ReactiveUI. The library has a navigation stack, not a history (no Forward), no route table
/// (the application creates the view model and gives it to <c>Navigate</c>) and no guard: the guard and the refusal to leave are written
/// in the application, in front of <c>Navigate</c>, the way the documentation suggests.
/// </summary>
public sealed class ReactiveUiDriver : INavigationDriver
{
    private readonly RxScreen _screen = new();
    private readonly IClock _clock = new Clock();

    public ReactiveUiDriver() => State = new AppState();

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
    }

    public async Task BackAsync()
    {
        if (_screen.Router.NavigationStack.Count > 1 && CanLeave())
            await _screen.Router.NavigateBack.Execute(Unit.Default);
    }

    public Task ForwardAsync() => throw new NotSupportedException("ReactiveUI routing has no Forward.");

    public Task<bool> ClearHistoryAsync()
    {
        var current = _screen.Router.NavigationStack.LastOrDefault();
        _screen.Router.NavigationStack.Clear();
        if (current is not null)
            _screen.Router.NavigationStack.Add(current);
        return Task.FromResult(true);
    }

    private bool CanLeave() => _screen.Router.NavigationStack.LastOrDefault() is not RxPage page || page.CanLeave();

    private RxPage Create(string[] parts) => parts[0] switch
    {
        ['C', ..] => new RxPage($"{parts[0]}:{parts[1]}", _screen),
        "Detail" => new RxPage($"Detail:{parts[1]}", _screen),
        "Edit" => new RxPage($"Edit:{parts[1]}", _screen) { CanLeave = () => !State.Dirty },
        ['P', '3', '0' or '1' or '2' or '3' or '4'] => PageWithDependency(parts[0]),
        _ => new RxPage(parts[0], _screen),
    };

    private RxPage PageWithDependency(string name)
    {
        _ = _clock.Now;   // the dependency of the five pages that have one
        return new RxPage(name, _screen);
    }

    public void Dispose()
    {
    }
}
