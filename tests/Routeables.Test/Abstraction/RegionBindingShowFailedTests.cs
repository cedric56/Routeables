using Routeables.Regions;
using Routeables.Test.Abstraction;
using static Routeables.Test.Abstraction.RouterTestKit;
using Page = Routeables.Test.Abstraction.Page;

namespace Routeables.Tests.Abstraction;

/// <summary>
/// A host that puts the view on its UI thread later (the route change came from another thread) is the only one that learns the view could not be
/// shown: it tells the binding, and the same view is shown again at the next refresh instead of being taken for shown.
/// </summary>
public class RegionBindingShowFailedTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Page NewPage(string name, CallLog log)
        => new(new RecordingViewModel($"vm{name}", log), new RecordingView($"view{name}", log));

    [Fact]
    public async Task A_view_reported_as_not_shown_is_shown_again_by_the_next_refresh()
    {
        var router = CreateRouter();
        var shown = new List<object?>();
        using var binding = new RegionBinding(router, view => shown.Add(view));
        var page = NewPage("A", new CallLog());
        await router.GoAsync(page.View, page.ViewModel, null, Ct);
        Assert.Same(page.View, Assert.Single(shown, v => v is not null));

        binding.Refresh();
        Assert.Equal(1, shown.Count(v => v is not null));

        binding.ShowFailed(page.View);
        binding.Refresh();

        Assert.Equal(2, shown.Count(v => ReferenceEquals(v, page.View)));
    }

    [Fact]
    public async Task A_report_about_a_view_that_is_no_longer_the_one_shown_changes_nothing()
    {
        var router = CreateRouter();
        var shown = new List<object?>();
        using var binding = new RegionBinding(router, view => shown.Add(view));
        var log = new CallLog();
        var first = NewPage("A", log);
        var second = NewPage("B", log);
        await router.GoAsync(first.View, first.ViewModel, null, Ct);
        await router.GoAsync(second.View, second.ViewModel, null, Ct);
        var count = shown.Count;

        binding.ShowFailed(first.View); // a late failure of the first view: the second is on screen
        binding.Refresh();

        Assert.Equal(count, shown.Count);
    }

    [Fact]
    public async Task A_report_after_the_binding_is_disposed_does_nothing()
    {
        var router = CreateRouter();
        var binding = new RegionBinding(router, _ => { });
        var page = NewPage("A", new CallLog());
        await router.GoAsync(page.View, page.ViewModel, null, Ct);
        binding.Dispose();

        binding.ShowFailed(page.View);
        binding.Refresh();
    }

    [Fact]
    public void A_connection_without_a_binding_ignores_the_report()
        => new RegionConnection().ShowFailed(new object());
}
