using Routeables.Abstraction;
using Routeables.Core;
using System.Collections.ObjectModel;

namespace Routeables.Tests.Core;

/// <summary>
/// Concrete <see cref="RouteableViewModelBase"/> used to test routing/history/caching behavior.
/// </summary>
/// <remarks>
/// <see cref="RouteableViewModelBase"/>'s own constructor can, when <c>initialize</c> is
/// <see langword="true"/>, synchronously select the first navigable item - which calls the abstract
/// <see cref="GetRouteables"/> *before* any field assigned in this type's constructor body has run
/// (base constructors always execute before derived constructor bodies). A normal constructor
/// parameter captured into a field would therefore be observed as not-yet-assigned the first time
/// <see cref="GetRouteables"/> is invoked. <see cref="Create"/> works around this by handing the
/// navigables collection to the instance via a <see cref="ThreadStaticAttribute"/> slot that is
/// populated immediately before construction and read by <see cref="GetRouteables"/> for as long as
/// it is set, falling back to the (by-then-assigned) field afterwards.
/// </remarks>
public class TestNavigableViewModel : RouteableViewModelBase
{
    [ThreadStatic]
    private static ObservableCollection<IRouteableEntry>? _pending;

    public static TestNavigableViewModel Create(IRouter router, ObservableCollection<IRouteableEntry> navigables, bool initialize = true)
    {
        _pending = navigables;
        try
        {
            return new TestNavigableViewModel(router, initialize);
        }
        finally
        {
            _pending = null;
        }
    }

    private readonly ObservableCollection<IRouteableEntry> _navigables;

    private TestNavigableViewModel(IRouter router, bool initialize)
        : base(router, initialize)
    {
        _navigables = _pending ?? [];
    }

    public List<string> NotifiedProperties { get; } = [];

    public int RaiseCanExecutesChangedCallCount { get; private set; }

    protected override ObservableCollection<IRouteableEntry> GetRouteables() => _pending ?? _navigables;

    protected override void NotifyPropertyChanged(string? propertyName)
    {
        NotifiedProperties.Add(propertyName ?? string.Empty);
    }

    protected override void RaiseCanExecutesChanged()
    {
        RaiseCanExecutesChangedCallCount++;
    }
}
