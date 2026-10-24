using Routeables.Abstraction;
using System.Windows.Input;
using Routeables.Locking;
using XAminals.Common;
using XAnimals.Common;

namespace XAnimals.ViewModels;

/// <summary>
/// The Lock page: locks and unlocks the menu the way an application does while it is busy or the user must finish something first.
/// <para>
/// A view model never touches the menu. It publishes a <see cref="RouteableLockMessage"/> on the <see cref="IRouteableBrokerService"/>;
/// the <see cref="ILockCoordinator"/> of the main view model (created in <see cref="MainViewModel"/>) receives it and sets
/// <see cref="IRouteableEntry.IsLock"/> on every entry, which disables them in Razor, Avalonia and Maui.
/// The page itself stays usable, so it can unlock the menu again.
/// </para>
/// </summary>
[Routeable("lock")]
public class LockViewModel : ViewModelBase, IDisposable
{
    /// <summary>How long "Lock for a few seconds" keeps the menu locked.</summary>
    public const int Seconds = 5;

    private readonly IRouteableBrokerService _broker;
    private CancellationTokenSource? _countdown;
    private bool _isLocked;
    private int _remaining;

    public LockViewModel(IRouteableBrokerService broker)
    {
        _broker = broker;

        LockCommand = new AsyncCommand(LockAsync);
        UnlockCommand = new AsyncCommand(UnlockAsync);
        LockForCommand = new AsyncCommand(LockForAsync);
    }

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
            OnPropertyChanged(nameof(Status));
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
            OnPropertyChanged(nameof(Status));
        }
    }

    public bool CanLock => !IsLocked;

    public bool CanUnlock => IsLocked;

    public string Status => (IsLocked, Remaining) switch
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

    /// <summary>Locks the menu for <see cref="Seconds"/> seconds, then unlocks it; unlocking earlier cancels the wait.</summary>
    public async Task LockForAsync()
    {
        StopCountdown();
        var countdown = _countdown = new CancellationTokenSource();

        Publish(locked: true);
        try
        {
            for (var left = Seconds; left > 0; left--)
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

    // IsAllLocked: false = this main view model only; true would also lock the other IMainRouteables of the application.
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
}
