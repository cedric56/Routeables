using Routeables.Abstraction.Bases;
using System.Collections.ObjectModel;

namespace Routeables.Tests.Core;

/// <summary>An <see cref="IRouteableMenuViewModel"/> that does nothing: the menu view model of a host, in tests that are about something else.</summary>
public sealed class FakeMenuViewModel : IRouteableMenuViewModel
{
    public ObservableCollection<IRouteableMenuEntry> Entries { get; } = [];
    public IRouteableMenuEntry? SelectedEntry { get; set; }
    public object? CurrentView { get; set; }
    public event EventHandler<object?>? CurrentViewChanged { add { } remove { } }

    private EventHandler? _lockChanged;
    public event EventHandler? LockChanged { add => _lockChanged += value; remove => _lockChanged -= value; }

    /// <summary>How many handlers follow <see cref="LockChanged"/>.</summary>
    public int LockSubscribers => _lockChanged?.GetInvocationList().Length ?? 0;

    public void NotifyLockChanged() => _lockChanged?.Invoke(this, EventArgs.Empty);

    public string? CurrentTitle { get; }
}
