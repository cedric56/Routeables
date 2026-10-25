using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Locking;
using Routeables.Services;
using System.Windows.Input;
using XAminals.Common;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>
/// The Features page: what the framework does around the route changes, each one with a button.
/// <list type="bullet">
/// <item><description><b>Guard</b>: the Members page is for members (<see cref="MembersGuard"/>). Signed out, it sends you here; sign in, then open it.</description></item>
/// <item><description><b>Link</b>: an address from outside (<c>xanimals://dogs/Eurohound</c>)followed  through the router (<see cref="RouteableDeepLinks"/>).</description></item>
/// <item><description><b>Lock</b>: the page locks and unlocks the menu the way an application does while it is busy (a <see cref="RouteableLockMessage"/> on the broker, received by the <see cref="ILockCoordinator"/> of the main view model).</description></item>
/// <item><description><b>Saved values</b>: the draft survives the process being killed (<see cref="IRouteableState"/>, <see cref="RouteableState"/>).</description></item>
/// <item><description><b>Measures</b>: the time of each route change, published by the router (<see cref="RouteMetrics"/>).</description></item>
/// </list>
/// The Back button of the system, the transitions of the regions and the links that start the application are in the heads.
/// </summary>
[Routeable("features")]
public class FeaturesViewModel : ViewModelBase, IRouteableState, IDisposable
{
    public const string SampleLink = "xanimals://dogs/Eurohound";

    private readonly IRouter _router;
    private readonly IMainViewModel _main;
    private readonly IServiceProvider _provider;
    private readonly MemberSession _session;
    private readonly IRouteableBrokerService _broker;
    private CancellationTokenSource? _countdown;
    private bool _isLocked;
    private int _remaining;
    private string _draft = string.Empty;
    private string? _status;
    private string _measures = string.Empty;

    public FeaturesViewModel(IRouter router, IMainViewModel main, IServiceProvider provider, MemberSession session, IRouteableBrokerService broker)
    {
        _router = router;
        _main = main;
        _provider = provider;
        _session = session;
        _broker = broker;

        SignInCommand = new AsyncCommand(SignInAsync);
        SignOutCommand = new AsyncCommand(SignOutAsync);
        OpenMembersCommand = new AsyncCommand(OpenMembersAsync);
        OpenLinkCommand = new AsyncCommand(OpenLinkAsync);
        RefreshCommand = new AsyncCommand(RefreshAsync);
        LockCommand = new AsyncCommand(LockAsync);
        UnlockCommand = new AsyncCommand(UnlockAsync);
        LockForCommand = new AsyncCommand(LockForAsync);
        Measures = Describe();
    }

    public ICommand SignInCommand { get; }
    public ICommand SignOutCommand { get; }
    public ICommand OpenMembersCommand { get; }
    public ICommand OpenLinkCommand { get; }
    public ICommand RefreshCommand { get; }

    public bool IsSignedIn => _session.IsSignedIn;

    public bool IsSignedOut => !_session.IsSignedIn;

    public string Status
    {
        // Before anything was done on the page: what brought the user here, or what to try.
        get => _status ?? "The Members page is for members: sign in, then open it.";
        private set { _status = value; OnPropertyChanged(); }
    }

    /// <summary>Whether the system can close the application and bring it back: the Saved values section is shown on a phone or tablet only.</summary>
    public bool IsMobile => _provider.GetService(typeof(IDesktop)) is IDesktop { Current: var d } && (d == Desktop.Android || d == Desktop.iOS);

    /// <summary>What the user typed; kept by <see cref="SaveState"/> when the system saves the pages.</summary>
    public string Draft
    {
        get => _draft;
        set { _draft = value ?? string.Empty; OnPropertyChanged(); }
    }

    // ---- Lock: the page publishes a RouteableLockMessage; the coordinator of the main view model disables the menu entries.

    /// <summary>How long "Lock for a few seconds" keeps the menu locked.</summary>
    public const int LockSeconds = 5;

    public ICommand LockCommand { get; }
    public ICommand UnlockCommand { get; }
    public ICommand LockForCommand { get; }

    /// <summary>Whether this page locked the menu (it is the page that publishes, so it knows).</summary>
    public bool IsLocked
    {
        get => _isLocked;
        private set
        {
            if (_isLocked == value) return;
            _isLocked = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanLock));
            OnPropertyChanged(nameof(CanUnlock));
            OnPropertyChanged(nameof(LockStatus));
        }
    }

    /// <summary>Seconds left before the menu opens again; 0 when no countdown runs.</summary>
    public int Remaining
    {
        get => _remaining;
        private set
        {
            if (_remaining == value) return;
            _remaining = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LockStatus));
        }
    }

    public bool CanLock => !IsLocked;

    public bool CanUnlock => IsLocked;

    public string LockStatus => (IsLocked, Remaining) switch
    {
        (true, > 0) => $"The menu is locked. It opens again in {Remaining} s.",
        (true, _) => "The menu is locked. Only this page can open it again.",
        _ => "The menu is open."
    };

    /// <summary>Locks every entry of the menu until <see cref="UnlockAsync"/>.</summary>
    public Task LockAsync()
    {
        StopCountdown();
        Publish(locked: true);
        return Task.CompletedTask;
    }

    /// <summary>Unlocks every entry of the menu.</summary>
    public Task UnlockAsync()
    {
        StopCountdown();
        Publish(locked: false);
        return Task.CompletedTask;
    }

    /// <summary>Locks the menu for <see cref="LockSeconds"/> seconds, then unlocks it; unlocking earlier cancels the wait.</summary>
    public async Task LockForAsync()
    {
        StopCountdown();
        var countdown = _countdown = new CancellationTokenSource();

        Publish(locked: true);
        try
        {
            for (var left = LockSeconds; left > 0; left--)
            {
                Remaining = left;
                await Task.Delay(TimeSpan.FromSeconds(1), countdown.Token);
            }

            Remaining = 0;
            Publish(locked: false);
        }
        catch (OperationCanceledException)
        {
            // Another lock or unlock took over: it has already published its own state.
        }
        finally
        {
            if (ReferenceEquals(_countdown, countdown))
                _countdown = null;
            countdown.Dispose();
        }
    }

    // IsAllLocked: false = this main view model only; true would also lock the other IMainViewModel of the application.
    private void Publish(bool locked)
    {
        _broker.Publish(new RouteableLockMessage(locked, IsAllLocked: false));
        IsLocked = locked;
    }

    private void StopCountdown()
    {
        _countdown?.Cancel();
        _countdown = null;
        Remaining = 0;
    }

    /// <summary>
    /// Stops a countdown that is still running, so it does not keep this view model alive until it ends. The lock of a
    /// countdown is temporary: it is lifted, or the menu would stay locked with no page left to unlock it.
    /// </summary>
    public void Dispose()
    {
        if (_countdown is null)
            return;

        StopCountdown();
        try { Publish(locked: false); }
        catch (ObjectDisposedException) { /* the broker's scope is already gone: nothing left to unlock */ }
    }

    public string Measures
    {
        get => _measures;
        private set { _measures = value; OnPropertyChanged(); }
    }

    public string Link => SampleLink;

    // The same actions as methods, for the Razor view.
    public Task SignInAsync()
    {
        _session.IsSignedIn = true;
        Changed();
        Status = "Signed in: Open Members now goes there.";
        return Task.CompletedTask;
    }

    public Task SignOutAsync()
    {
        _session.IsSignedIn = false;
        Changed();
        return Task.CompletedTask;
    }

    public Task OpenMembersAsync()
    {
        return GoAsync("/members");
    }

    public async Task OpenLinkAsync()
    {
        var result = await RouteableDeepLinks.GoToAsync(_provider, new Uri(SampleLink), ["xanimals"]);
        Status = result.Success ? $"Followed {SampleLink}." : $"Could not follow {SampleLink}: {result.Exception?.Message}";
    }

    public Task RefreshAsync()
    {
        Measures = Describe();
        Changed();
        return Task.CompletedTask;
    }

    private async Task GoAsync(string route)
    {
        var result = await _router.GoToAsync(route);

        // The router moved, not the menu: it shows the page that is now current and selects its entry.
        if (result.Success)
            _main.OnRouterMoved(_router.CurrentViewModel);

        Status = result.Success
            ? $"Went to {route}."
            : result.Cancelled ? $"{route} was refused." : $"Could not go to {route}: {result.Exception?.Message}";
        Changed();
    }

    private void Changed()
    {
        OnPropertyChanged(nameof(IsSignedIn));
        OnPropertyChanged(nameof(IsSignedOut));
    }

    private static string Describe()
    {
        var lines = RouteMetrics.Last();
        return lines.Count == 0 ? "No route change measured yet." : string.Join(Environment.NewLine, lines);
    }

    public void SaveState(IDictionary<string, string> state) => state["draft"] = Draft;

    public void LoadState(IReadOnlyDictionary<string, string> state)
    {
        if (state.TryGetValue("draft", out var draft))
            Draft = draft;
    }
}
