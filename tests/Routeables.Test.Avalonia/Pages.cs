using Avalonia.Controls;
using Routeables.Avalonia;

namespace Routeables.Test.Avalonia;

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