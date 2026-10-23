using Routeables.Abstraction;
using System.Collections.ObjectModel;

namespace Routeables.Tests.Core;

/// <summary>An <see cref="IMainRouteables"/> that does nothing: the main view model of a host, in tests that are about something else.</summary>
public sealed class FakeMainRouteables : IMainRouteables
{
    public ObservableCollection<IRouteableEntry> Routeables { get; } = [];
    public IRouteableEntry? Routeable { get; set; }
    public object? CurrentView { get; set; }
    public EventHandler<object?>? CurrentViewChanged { get; set; }
    public EventHandler? LockChanged { get; set; }
}
