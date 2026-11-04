using Routeables.Abstraction;
using Routeables.Uno;
using System.Collections.ObjectModel;

namespace Routeables.Test.Uno;

/// <summary>The main view model of the test application: the controls under test need one, not its menu.</summary>
public sealed class TestMain : IMainViewModel
{
    /// <summary>The collection of available routeable entries.</summary>
    public ObservableCollection<IRouteableEntry> Routeables { get; } = [];

    /// <summary>The currently selected routeable entry, if any.</summary>
    public IRouteableEntry? Routeable { get; set; }

    /// <summary>The view currently displayed by the main content area.</summary>
    public object? CurrentView { get; set; }

    /// <summary>Raised when <see cref="CurrentView"/> changes. Empty implementation for headless tests.</summary>
    public event EventHandler<object?>? CurrentViewChanged { add { } remove { } }

    /// <summary>Raised when the lock state changes. Empty implementation for headless tests.</summary>
    public event EventHandler? LockChanged { add { } remove { } }

    /// <summary>The title of the current view. Always null in this test fixture.</summary>
    public string? CurrentTitle { get; }
}

/// <summary>View model for the test page A. Exposes a flag that controls whether the page may be left.</summary>
public sealed class PageAViewModel
{
    /// <summary>When <see langword="false"/> the page refuses to be left.</summary>
    public bool CanLeave { get; set; } = true;
}

/// <summary>View model for the test page B. Has no additional state.</summary>
public sealed class PageBViewModel;

/// <summary>A page written in code: a text, so a test can tell which page a region shows.</summary>
public sealed class PageAView : RouteableFor<PageAViewModel>
{
    /// <summary>Builds the page with a simple "A" label.</summary>
    public PageAView() => Content = new TextBlock { Text = "A" };

    /// <inheritdoc />
    public override Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(ViewModel.CanLeave);
}

/// <summary>A page written in code: a text, so a test can tell which page a region shows.</summary>
public sealed class PageBView : RouteableFor<PageBViewModel>
{
    /// <summary>Builds the page with a simple "B" label.</summary>
    public PageBView() => Content = new TextBlock { Text = "B" };
}