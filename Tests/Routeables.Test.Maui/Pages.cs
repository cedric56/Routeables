using System.Collections.ObjectModel;
using Routeables.Abstraction;
using Routeables.Maui;

namespace Routeables.Test.Maui;

/// <summary>The main view model of the test application: the controls under test need one, not its menu.</summary>
public sealed class TestMain : IMainViewModel
{
    public ObservableCollection<IRouteableEntry> Routeables { get; } = [];
    public IRouteableEntry? Routeable { get; set; }
    public object? CurrentView { get; set; }
    public event EventHandler<object?>? CurrentViewChanged { add { } remove { } }
    public event EventHandler? LockChanged { add { } remove { } }
    public string? CurrentTitle { get; }
}

public sealed class PageAViewModel
{
    /// <summary>When <see langword="false"/> the page refuses to be left.</summary>
    public bool CanLeave { get; set; } = true;
}

public sealed class PageBViewModel;

/// <summary>A page written in code: a text, so a test can tell which page a region shows.</summary>
public sealed class PageAView : RouteableFor<PageAViewModel>
{
    public PageAView() => Content = new Label { Text = "A" };

    public override Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(ViewModel.CanLeave);
}

public sealed class PageBView : RouteableFor<PageBViewModel>
{
    public PageBView() => Content = new Label { Text = "B" };
}
