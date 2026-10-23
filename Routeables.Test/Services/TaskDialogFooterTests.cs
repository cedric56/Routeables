using Routeables.Services;

namespace Routeables.Tests.Services;

public class TaskDialogFooterTests
{
    [Theory]
    [InlineData(RouteableTaskDialogFooterVisibility.Never, false, false, false, false)]
    [InlineData(RouteableTaskDialogFooterVisibility.Auto, false, true, true, false)]
    [InlineData(RouteableTaskDialogFooterVisibility.Auto, true, true, true, true)]
    [InlineData(RouteableTaskDialogFooterVisibility.Always, false, true, false, true)]
    public void The_footer_rules_are_the_same_for_every_host(
        RouteableTaskDialogFooterVisibility visibility, bool expanded, bool shows, bool collapsible, bool startsOpen)
    {
        var dialog = new RouteableTaskDialog { Footer = "details", FooterVisibility = visibility, IsFooterExpanded = expanded };

        Assert.Equal((shows, collapsible), (dialog.ShowsFooter, dialog.IsFooterCollapsible));
        Assert.Equal(startsOpen, dialog.FooterStartsExpanded);
    }

    [Fact]
    public void Without_a_footer_nothing_is_shown()
    {
        var dialog = new RouteableTaskDialog { FooterVisibility = RouteableTaskDialogFooterVisibility.Always };

        Assert.False(dialog.ShowsFooter);
        Assert.False(dialog.IsFooterCollapsible);
    }
}
