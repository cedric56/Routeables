using Routeables.Abstraction;
using Routeables.Core;
using System.Collections.ObjectModel;

namespace Routeables.Tests.Core;

/// <summary>
/// Concrete <see cref="MainViewModelBase"/> used to test routing/history/caching behavior.
/// </summary>
/// <remarks>
/// <see cref="MainViewModelBase"/>'s own constructor can, when <c>initialize</c> is
/// <see langword="true"/>, synchronously select the first navigable item - which calls the abstract
/// <see cref="GetEntries"/> *before* any field assigned in this type's constructor body has run
/// (base constructors always execute before derived constructor bodies). A normal constructor
/// parameter captured into a field would therefore be observed as not-yet-assigned the first time
/// <see cref="GetEntries"/> is invoked. <see cref="Create"/> works around this by handing the
/// navigables collection to the instance via a <see cref="ThreadStaticAttribute"/> slot that is
/// populated immediately before construction and read by <see cref="GetEntries"/> for as long as
/// it is set, falling back to the (by-then-assigned) field afterwards.
/// </remarks>
public class TestNavigableViewModel : MainViewModelBase
{
    [ThreadStatic]
    private static ObservableCollection<IRouteableMenuEntry>? _pending;

    public static TestNavigableViewModel Create(IRouter router, ObservableCollection<IRouteableMenuEntry> navigables, bool initialize = true)
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

    private readonly ObservableCollection<IRouteableMenuEntry> _navigables;

    private TestNavigableViewModel(IRouter router, bool initialize)
        : base(router, initialize)
    {
        _navigables = _pending ?? [];
    }

    public List<string> NotifiedProperties { get; } = [];

    public int RaiseCanExecutesChangedCallCount { get; private set; }

    protected override ObservableCollection<IRouteableMenuEntry> GetEntries() => _pending ?? _navigables;

    protected override void NotifyPropertyChanged(string? propertyName)
    {
        NotifiedProperties.Add(propertyName ?? string.Empty);
    }

    protected override void RaiseCanExecutesChanged()
    {
        RaiseCanExecutesChangedCallCount++;
    }
}
