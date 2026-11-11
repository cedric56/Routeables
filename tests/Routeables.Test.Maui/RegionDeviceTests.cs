using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
using Routeables.Abstraction.Transitions;
using Routeables.Maui;
using Xunit;

namespace Routeables.Test.Maui;

/// <summary>
/// <see cref="RouteableRegion"/> with a real handler on the platform: it shows what the router of its region displays, follows Back and
/// the refusals, lets go of the view when it leaves the screen, and plays its transitions without ever leaving the view off to the side.
/// </summary>
public class RegionDeviceTests
{
    /// <summary>
    /// Creates a fresh device environment and hosts a configured routeable region.
    /// </summary>
    private static async Task<Hosted<RouteableRegion>> HostAsync(Action<RouteableRegion>? configure = null)
    {
        await Device.ResetAsync();
        var region = new RouteableRegion();
        configure?.Invoke(region);
        return new Hosted<RouteableRegion>(region);
    }

    /// <summary>
    /// Verifies that navigating to a route displays its view and expected view model.
    /// </summary>
    [UIFact]
    public async Task A_region_shows_the_view_of_the_route()
    {
        using var host = await HostAsync();

        var result = await Device.Services.GoToAsync("/a");

        Assert.True(result.Success);
        await Device.UntilAsync(() => host.View.Content is PageAView);
        Assert.IsType<PageAViewModel>(((PageAView)host.View.Content!).BindingContext);
    }

    /// <summary>
    /// Verifies that a region with no active route has no content.
    /// </summary>
    [UIFact]
    public async Task An_empty_region_shows_nothing()
    {
        using var host = await HostAsync();

        Assert.Null(host.View.Content);
    }

    /// <summary>
    /// Verifies that a region attached after navigation immediately displays the active route.
    /// </summary>
    [UIFact]
    public async Task A_region_attached_after_the_route_shows_the_current_view_at_once()
    {
        await Device.ResetAsync();
        await Device.Services.GoToAsync("/a");

        using var host = new Hosted<RouteableRegion>(new RouteableRegion());

        await Device.UntilAsync(() => host.View.Content is PageAView);
    }

    /// <summary>
    /// Verifies that Back and Forward navigation restore the expected views.
    /// </summary>
    [UIFact]
    public async Task Going_back_and_forward_shows_the_previous_and_the_next_view()
    {
        using var host = await HostAsync();
        await Device.Services.GoToAsync("/a");
        await Device.Services.GoToAsync("/b");
        await Device.UntilAsync(() => host.View.Content is PageBView);

        Assert.True((await Device.Router().GoBackAsync()).Success);
        await Device.UntilAsync(() => host.View.Content is PageAView);

        Assert.True((await Device.Router().ForwardAsync()).Success);
        await Device.UntilAsync(() => host.View.Content is PageBView);
    }

    /// <summary>
    /// Verifies that a page that refuses navigation remains visible until it allows leaving.
    /// </summary>
    [UIFact]
    public async Task A_page_that_refuses_to_be_left_stays_on_screen()
    {
        using var host = await HostAsync();
        await Device.Services.GoToAsync("/a");
        await Device.UntilAsync(() => host.View.Content is PageAView);
        var page = (PageAView)host.View.Content!;
        page.ViewModel.CanLeave = false;

        var refused = await Device.Services.GoToAsync("/b");

        Assert.False(refused.Success);
        Assert.Same(page, host.View.Content);

        page.ViewModel.CanLeave = true;
        Assert.True((await Device.Services.GoToAsync("/b")).Success);
        await Device.UntilAsync(() => host.View.Content is PageBView);
    }

    /// <summary>
    /// Verifies that each region displays the view associated with its own router.
    /// </summary>
    [UIFact]
    public async Task Each_region_shows_its_own_view()
    {
        using var main = await HostAsync();
        using var side = new Hosted<RouteableRegion>(new RouteableRegion { Region = MauiProgram.Side });

        await Device.Services.GoToAsync("/a");
        await Device.Services.GoToAsync("/b", MauiProgram.Side);

        await Device.UntilAsync(() => main.View.Content is PageAView && side.View.Content is PageBView);
    }

    /// <summary>
    /// Verifies that erasing navigation history clears the region's content.
    /// </summary>
    [UIFact]
    public async Task Erasing_the_history_empties_the_region()
    {
        using var host = await HostAsync();
        await Device.Services.GoToAsync("/a");
        await Device.UntilAsync(() => host.View.Content is PageAView);

        await Device.Router().EraseHistoryAsync();

        await Device.UntilAsync(() => host.View.Content is null);
    }

    /// <summary>
    /// Verifies that disposing a region clears its view and stops router updates.
    /// </summary>
    [UIFact]
    public async Task A_region_that_leaves_the_screen_lets_go_of_the_view_and_stops_following_the_router()
    {
        var host = await HostAsync();
        await Device.Services.GoToAsync("/a");
        await Device.UntilAsync(() => host.View.Content is PageAView);
        var region = host.View;

        host.Dispose();

        Assert.Null(region.Content);
        await Device.Services.GoToAsync("/b");
        await Task.Delay(100);
        Assert.Null(region.Content);
    }

    /// <summary>
    /// Verifies that reattaching a detached region displays the current route again.
    /// </summary>
    [UIFact]
    public async Task A_region_that_comes_back_shows_the_current_view_again()
    {
        var host = await HostAsync();
        await Device.Services.GoToAsync("/a");
        await Device.UntilAsync(() => host.View.Content is PageAView);
        var region = host.View;
        host.Dispose();

        Device.Attach(region);
        try
        {
            await Device.UntilAsync(() => region.Content is PageAView);
        }
        finally
        {
            Device.Detach(region);
        }
    }

    /// <summary>
    /// Verifies that changing the region switches to the selected router and that clearing the selection restores the default region.
    /// </summary>
    [UIFact]
    public async Task Changing_the_region_while_displayed_follows_the_other_router()
    {
        using var host = await HostAsync();
        await Device.Services.GoToAsync("/a");
        await Device.Services.GoToAsync("/b", MauiProgram.Side);
        await Device.UntilAsync(() => host.View.Content is PageAView);

        host.View.Region = MauiProgram.Side;
        await Device.UntilAsync(() => host.View.Content is PageBView);

        host.View.Region = null;
        await Device.UntilAsync(() => host.View.Content is PageAView);
    }

    /// <summary>
    /// Verifies that a view is displayed by only one region at a time.
    /// </summary>
    [UIFact]
    public async Task The_last_region_to_show_a_view_takes_it_from_another_region_of_the_same_router()
    {
        using var first = await HostAsync();
        using var second = new Hosted<RouteableRegion>(new RouteableRegion());

        await Device.Services.GoToAsync("/a");

        await Device.UntilAsync(() => first.View.Content is PageAView || second.View.Content is PageAView);
        // MAUI views can have only one parent, so exactly one region should own this view.
        Assert.Equal(1, new[] { first.View.Content, second.View.Content }.OfType<PageAView>().Count());
    }

    /// <summary>
    /// Verifies that a view is removed from its previous layout before being displayed by the region again.
    /// </summary>
    [UIFact]
    public async Task A_view_that_sits_in_a_layout_is_taken_out_of_it()
    {
        using var host = await HostAsync();
        await Device.Services.GoToAsync("/a");
        await Device.UntilAsync(() => host.View.Content is PageAView);
        var page = (PageAView)host.View.Content!;
        host.View.Content = null;
        var grid = new Grid();
        grid.Add(page);

        await Device.Services.GoToAsync("/b");
        await Device.Router().GoBackAsync();

        await Device.UntilAsync(() => host.View.Content is PageAView);
        Assert.DoesNotContain(page, grid.Children);
    }

    // ------------------------------------------------------------------------------------------------ transitions

    /// <summary>
    /// Returns the names of all built-in transitions for the parameterized transition tests.
    /// </summary>
    public static TheoryData<string> BuiltInNames()
    {
        var data = new TheoryData<string>();
        foreach (var transition in RouteTransition.BuiltIn)
            data.Add(transition.Name);
        return data;
    }

    /// <summary>
    /// Verifies that every built-in region transition completes with the expected view displayed and the region at rest.
    /// </summary>
    [UITheory]
    [MemberData(nameof(BuiltInNames))]
    public async Task Every_built_in_transition_of_the_region_ends_with_the_view_shown_and_the_region_at_rest(string name)
    {
        using var host = await HostAsync(r => r.Transition = name);
        await Device.Services.GoToAsync("/a");

        await Device.Services.GoToAsync("/b");

        await Device.UntilAsync(() => host.View.Content is PageBView && Device.IsAtRest(host.View), describe: () => $"Transition '{name}': {Device.State(host.View)}, region content: {host.View.Content?.GetType().Name}");
        await Device.Router().GoBackAsync();
        await Device.UntilAsync(() => host.View.Content is PageAView && Device.IsAtRest(host.View), describe: () => $"Transition '{name}' (back): {Device.State(host.View)}, region content: {host.View.Content?.GetType().Name}");
    }

    /// <summary>
    /// Verifies that every built-in transition requested during navigation completes with the region at rest.
    /// </summary>
    [UITheory]
    [MemberData(nameof(BuiltInNames))]
    public async Task Every_built_in_transition_asked_by_the_navigation_ends_at_rest(string name)
    {
        using var host = await HostAsync();
        await Device.Services.GoToAsync("/a");

        await Device.Services.GoToAsync("/b", Device.With(RouteTransition.Named(name).WithDuration(TimeSpan.FromMilliseconds(80))));

        await Device.UntilAsync(() => host.View.Content is PageBView && Device.IsAtRest(host.View));
    }

    /// <summary>
    /// Verifies that navigation without a transition leaves the region's opacity unchanged.
    /// </summary>
    [UIFact]
    public async Task A_region_without_transition_never_touches_its_opacity()
    {
        using var host = await HostAsync();
        host.View.Opacity = 0.5;

        await Device.Services.GoToAsync("/a");
        await Device.UntilAsync(() => host.View.Content is PageAView);

        Assert.Equal(0.5, host.View.Opacity);
    }

    /// <summary>
    /// Verifies that the region's legacy transition duration still animates the view into place.
    /// </summary>
    [UIFact]
    public async Task The_old_duration_of_the_region_fades_the_view_in()
    {
        using var host = await HostAsync(r => r.TransitionDuration = TimeSpan.FromMilliseconds(120));

        await Device.Services.GoToAsync("/a");

        await Device.UntilAsync(() => host.View.Content is PageAView && Device.IsAtRest(host.View));
    }

    /// <summary>
    /// Verifies that an explicit None transition on navigation overrides the region's configured transition.
    /// </summary>
    [UIFact]
    public async Task None_on_the_navigation_stops_the_transition_of_the_region()
    {
        using var host = await HostAsync(r => r.Transition = "Fade");
        await Device.Services.GoToAsync("/a");
        await Device.UntilAsync(() => Device.IsAtRest(host.View));

        await Device.Services.GoToAsync("/b", Device.With(RouteTransition.None));

        await Device.UntilAsync(() => host.View.Content is PageBView);
        Assert.True(Device.IsAtRest(host.View));
    }

    /// <summary>
    /// Verifies that an unknown transition name displays the view immediately without failing.
    /// </summary>
    [UIFact]
    public async Task An_unknown_transition_name_shows_the_view_at_once_without_failing()
    {
        using var host = await HostAsync(r => r.Transition = "NobodyKnowsThisOne");

        await Device.Services.GoToAsync("/a");

        await Device.UntilAsync(() => host.View.Content is PageAView);
        Assert.True(Device.IsAtRest(host.View));
    }

    /// <summary>
    /// Verifies that rapid navigation during long transitions leaves the final view displayed and the region at rest.
    /// </summary>
    [UIFact]
    public async Task Rapid_changes_with_a_long_transition_leave_the_region_at_rest_and_on_the_last_view()
    {
        using var host = await HostAsync(r => r.Transition = "Slide");
        host.View.TransitionDuration = TimeSpan.FromSeconds(2);

        for (var i = 0; i < 6; i++)
            await Device.Services.GoToAsync(i % 2 == 0 ? "/a" : "/b", Device.With(RouteTransition.Slide.WithDuration(TimeSpan.FromMilliseconds(400))));

        await Device.UntilAsync(() => host.View.Content is PageBView && Device.IsAtRest(host.View), seconds: 10);
    }

    /// <summary>
    /// Verifies that disposing a region during an active transition does not throw an exception.
    /// </summary>
    [UIFact]
    public async Task A_region_that_leaves_the_screen_while_a_transition_plays_does_not_throw()
    {
        var host = await HostAsync(r => r.Transition = "Flip");
        await Device.Services.GoToAsync("/a");
        await Device.Services.GoToAsync("/b", Device.With(RouteTransition.Flip.WithDuration(TimeSpan.FromSeconds(2))));

        host.Dispose();

        await Task.Delay(150);
        Assert.Null(host.View.Content);
    }
}
