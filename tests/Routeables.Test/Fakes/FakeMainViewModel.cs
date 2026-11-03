using Routeables.Abstraction;
using System.Collections.ObjectModel;

namespace Routeables.Tests.Core;

/// <summary>An <see cref="IMainViewModel"/> that does nothing: the main view model of a host, in tests that are about something else.</summary>
public sealed class FakeMainViewModel : IMainViewModel
{
    public ObservableCollection<IRouteableEntry> Routeables { get; } = [];
    public IRouteableEntry? Routeable { get; set; }
    public object? CurrentView { get; set; }
    public event EventHandler<object?>? CurrentViewChanged { add { } remove { } }

    private EventHandler? _lockChanged;
    public event EventHandler? LockChanged { add => _lockChanged += value; remove => _lockChanged -= value; }

    /// <summary>How many handlers follow <see cref="LockChanged"/>.</summary>
    public int LockSubscribers => _lockChanged?.GetInvocationList().Length ?? 0;

    public void NotifyLockChanged() => _lockChanged?.Invoke(this, EventArgs.Empty);

    public string? CurrentTitle { get; }
}
