using Routeables.Abstraction;
using Routeables.Maui;
using System.Collections.ObjectModel;

namespace Routeables.Test.Maui;

/// <summary>The main view model of the test application: the controls under test need one, not its menu.</summary>
public sealed class TestMain : IMainViewModel
{
    /// <summary>The entries of the menu: empty, since only the presence of a main view model matters to the tests.</summary>
    public ObservableCollection<IRouteableMenuEntry> Entries { get; } = [];

    /// <summary>The selected entry: never set by the tests, the controls under test do not drive it here.</summary>
    public IRouteableMenuEntry? SelectedEntry { get; set; }

    /// <summary>The current view: the tests read it, nothing notifies its changes.</summary>
    public object? CurrentView { get; set; }

    /// <summary>Nobody subscribes to it: the events of the interface are satisfied, not used.</summary>
    public event EventHandler<object?>? CurrentViewChanged { add { } remove { } }

    /// <summary>Nobody subscribes to it: the events of the interface are satisfied, not used.</summary>
    public event EventHandler? LockChanged { add { } remove { } }

    /// <summary>The current title: always null, the tests do not need one.</summary>
    public string? CurrentTitle { get; }
}

/// <summary>The view model of page A: it can refuse to be left, so a test can block a navigation.</summary>
public sealed class PageAViewModel
{
    /// <summary>When <see langword="false"/> the page refuses to be left.</summary>
    public bool CanLeave { get; set; } = true;
}

/// <summary>The view model of page B: it has nothing to say, it only exists to have a type.</summary>
public sealed class PageBViewModel;

/// <summary>A page written in code: a text, so a test can tell which page a region shows.</summary>
public sealed class PageAView : RouteableFor<PageAViewModel>
{
    /// <summary>The page shows the letter A, and lets the tests refuse to leave through its view model.</summary>
    public PageAView() => Content = new Label { Text = "A" };

    /// <summary>Answers with the will of the view model: <see langword="false"/> refuses to leave the page.</summary>
    public override Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(ViewModel.CanLeave);
}

/// <summary>The page of B, written in code as A is: only its letter tells it apart in a test.</summary>
public sealed class PageBView : RouteableFor<PageBViewModel>
{
    /// <summary>The page shows the letter B, so a test can tell it from page A.</summary>
    public PageBView() => Content = new Label { Text = "B" };
}