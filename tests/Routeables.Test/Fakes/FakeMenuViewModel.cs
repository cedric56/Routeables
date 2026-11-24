using Routeables.Abstraction.Bases;
using System.Collections.ObjectModel;

namespace Routeables.Tests.Core;

/// <summary>An <see cref="IRouteableMenuViewModel"/> that does nothing: the menu view model of a host, in tests that are about something else.</summary>
public sealed class FakeMenuViewModel : IRouteableMenuViewModel
{
    public ObservableCollection<IRouteableMenuEntry> Entries { get; } = [];
    public IRouteableMenuEntry? SelectedEntry { get; set; }
    public object? CurrentView { get; set; }
    private EventHandler<object?>? _viewChanged;
    public event EventHandler<object?>? CurrentViewChanged { add => _viewChanged += value; remove => _viewChanged -= value; }

    /// <summary>How many handlers follow <see cref="CurrentViewChanged"/>.</summary>
    public int ViewSubscribers => _viewChanged?.GetInvocationList().Length ?? 0;

    /// <summary>Says that the view shown changed.</summary>
    public void NotifyViewChanged(object? view) => _viewChanged?.Invoke(this, view);

    private EventHandler? _lockChanged;
    public event EventHandler? LockChanged { add => _lockChanged += value; remove => _lockChanged -= value; }

    /// <summary>How many handlers follow <see cref="LockChanged"/>.</summary>
    public int LockSubscribers => _lockChanged?.GetInvocationList().Length ?? 0;

    public void NotifyLockChanged() => _lockChanged?.Invoke(this, EventArgs.Empty);

    public string? CurrentTitle { get; }
}
