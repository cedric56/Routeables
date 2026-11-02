using Avalonia.Controls;
using Routeables.Avalonia;

namespace Routeables.Test.Avalonia;

public sealed class PageAViewModel
{
    /// <summary>When <see langword="false"/> the page refuses to be left.</summary>
    public bool CanLeave { get; set; } = true;
}

public sealed class PageBViewModel;

/// <summary>A page written in code: a text, so a test can tell which page a region shows.</summary>
public sealed class PageAView : RouteableFor<PageAViewModel>
{
    public PageAView() => Content = new TextBlock { Text = "A" };

    public override Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(ViewModel.CanLeave);
}

public sealed class PageBView : RouteableFor<PageBViewModel>
{
    public PageBView() => Content = new TextBlock { Text = "B" };
}
