using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Routeables.Abstraction;
using Routeables.Uno;
using Uno.UI.RuntimeTests;

namespace Routeables.Test.Uno.Tests;

/// <summary><see cref="RouteableRegion"/> in the tree of a real window: it shows what the router of its region displays and follows it.</summary>
[TestClass]
[RunsOnUIThread]
public class RegionTests
{
    private static async Task<RouteableRegion> LoadedRegionAsync(Action<RouteableRegion>? configure = null)
    {
        await TestHost.ResetAsync();
        var region = new RouteableRegion();
        configure?.Invoke(region);
        return await TestHost.LoadAsync(region);
    }

    [TestMethod]
    public async Task A_region_shows_the_view_of_the_route()
    {
        var region = await LoadedRegionAsync();
        try
        {
            var result = await TestHost.Services.GoToAsync("/a");

            Assert.IsTrue(result.Success);
            await TestHost.UntilAsync(() => region.Content is PageAView);
            Assert.IsNotNull(((PageAView)region.Content!).ViewModel);
        }
        finally { await TestHost.UnloadAsync(region); }
    }

    [TestMethod]
    public async Task An_empty_region_shows_nothing()
    {
        var region = await LoadedRegionAsync();
        try { Assert.IsNull(region.Content); }
        finally { await TestHost.UnloadAsync(region); }
    }

    [TestMethod]
    public async Task A_region_loaded_after_the_route_shows_the_current_view_at_once()
    {
        await TestHost.ResetAsync();
        await TestHost.Services.GoToAsync("/a");
        var region = await TestHost.LoadAsync(new RouteableRegion());
        try { await TestHost.UntilAsync(() => region.Content is PageAView); }
        finally { await TestHost.UnloadAsync(region); }
    }

    [TestMethod]
    public async Task Going_back_and_forward_shows_the_previous_and_the_next_view()
    {
        var region = await LoadedRegionAsync();
        try
        {
            await TestHost.Services.GoToAsync("/a");
            await TestHost.Services.GoToAsync("/b");
            await TestHost.UntilAsync(() => region.Content is PageBView);

            Assert.IsTrue((await TestHost.Router().GoBackAsync()).Success);
            await TestHost.UntilAsync(() => region.Content is PageAView);

            Assert.IsTrue((await TestHost.Router().ForwardAsync()).Success);
            await TestHost.UntilAsync(() => region.Content is PageBView);
        }
        finally { await TestHost.UnloadAsync(region); }
    }

    [TestMethod]
    public async Task A_page_that_refuses_to_be_left_stays_on_screen()
    {
        var region = await LoadedRegionAsync();
        try
        {
            await TestHost.Services.GoToAsync("/a");
            await TestHost.UntilAsync(() => region.Content is PageAView);
            var page = (PageAView)region.Content!;
            page.ViewModel.CanLeave = false;

            var refused = await TestHost.Services.GoToAsync("/b");

            Assert.IsFalse(refused.Success);
            Assert.AreSame(page, region.Content);

            page.ViewModel.CanLeave = true;
            Assert.IsTrue((await TestHost.Services.GoToAsync("/b")).Success);
            await TestHost.UntilAsync(() => region.Content is PageBView);
        }
        finally { await TestHost.UnloadAsync(region); }
    }

    [TestMethod]
    public async Task Each_region_shows_its_own_view()
    {
        var main = await LoadedRegionAsync();
        var side = await TestHost.LoadAsync(new RouteableRegion { Region = App.Side });
        try
        {
            await TestHost.Services.GoToAsync("/a");
            await TestHost.Services.GoToAsync("/b", App.Side);

            await TestHost.UntilAsync(() => main.Content is PageAView && side.Content is PageBView);
        }
        finally
        {
            await TestHost.UnloadAsync(main);
            await TestHost.UnloadAsync(side);
        }
    }

    [TestMethod]
    public async Task Erasing_the_history_empties_the_region()
    {
        var region = await LoadedRegionAsync();
        try
        {
            await TestHost.Services.GoToAsync("/a");
            await TestHost.UntilAsync(() => region.Content is PageAView);

            await TestHost.Router().EraseHistoryAsync();

            await TestHost.UntilAsync(() => region.Content is null);
        }
        finally { await TestHost.UnloadAsync(region); }
    }

    [TestMethod]
    public async Task A_region_that_leaves_the_tree_lets_go_of_the_view_and_stops_following_the_router()
    {
        var region = await LoadedRegionAsync();
        await TestHost.Services.GoToAsync("/a");
        await TestHost.UntilAsync(() => region.Content is PageAView);

        await TestHost.UnloadAsync(region);

        Assert.IsNull(region.Content);
        await TestHost.Services.GoToAsync("/b");
        await Task.Delay(100);
        Assert.IsNull(region.Content);
    }

    [TestMethod]
    public async Task A_region_that_comes_back_shows_the_current_view_again()
    {
        var region = await LoadedRegionAsync();
        await TestHost.Services.GoToAsync("/a");
        await TestHost.UntilAsync(() => region.Content is PageAView);
        await TestHost.UnloadAsync(region);

        await TestHost.LoadAsync(region);
        try { await TestHost.UntilAsync(() => region.Content is PageAView); }
        finally { await TestHost.UnloadAsync(region); }
    }

    [TestMethod]
    public async Task Changing_the_region_while_loaded_follows_the_other_router()
    {
        var region = await LoadedRegionAsync();
        try
        {
            await TestHost.Services.GoToAsync("/a");
            await TestHost.Services.GoToAsync("/b", App.Side);
            await TestHost.UntilAsync(() => region.Content is PageAView);

            region.Region = App.Side;
            await TestHost.UntilAsync(() => region.Content is PageBView);

            region.Region = null;
            await TestHost.UntilAsync(() => region.Content is PageAView);
        }
        finally { await TestHost.UnloadAsync(region); }
    }

    [TestMethod]
    public async Task A_view_held_by_another_host_gives_an_error_that_names_who_holds_it()
    {
        var region = await LoadedRegionAsync();
        var other = new ContentControl();
        await TestHost.LoadAsync(other);
        var page = new PageAView();
        other.Content = page;
        try
        {
            // The router is asked to display a view that a host which is not a region holds: the region cannot show it too, and says so.
            string? message = null;
            try
            {
                var result = await TestHost.Router().GoAsync(page, new PageAViewModel());
                message = result.Exception?.ToString();
            }
            catch (InvalidOperationException ex)
            {
                message = ex.ToString();
            }

            Assert.IsNotNull(message);
            StringAssert.Contains(message, "already displayed");
            Assert.AreSame(page, other.Content);
        }
        finally
        {
            other.Content = null;
            await TestHost.UnloadAsync(other);
            await TestHost.UnloadAsync(region);
        }
    }

    // ------------------------------------------------------------------------------------------------ transitions

    [TestMethod]
    public async Task Every_built_in_transition_of_the_region_shows_the_pages_going_forward_and_back()
    {
        foreach (var transition in RouteTransition.BuiltIn)
        {
            var region = await LoadedRegionAsync(r => r.RouteTransition = transition.Name);
            try
            {
                await TestHost.Services.GoToAsync("/a");
                await TestHost.Services.GoToAsync("/b");
                await TestHost.UntilAsync(() => region.Content is PageBView);

                await TestHost.Router().GoBackAsync();
                await TestHost.UntilAsync(() => region.Content is PageAView);
            }
            finally { await TestHost.UnloadAsync(region); }
        }
    }

    [TestMethod]
    public async Task Every_built_in_transition_asked_by_the_navigation_shows_the_page()
    {
        foreach (var transition in RouteTransition.BuiltIn)
        {
            var region = await LoadedRegionAsync();
            try
            {
                await TestHost.Services.GoToAsync("/a");
                await TestHost.Services.GoToAsync("/b", TestHost.With(transition));

                await TestHost.UntilAsync(() => region.Content is PageBView);
            }
            finally { await TestHost.UnloadAsync(region); }
        }
    }

    [TestMethod]
    public async Task The_transition_of_the_navigation_wins_over_the_one_of_the_region()
    {
        var region = await LoadedRegionAsync(r => r.RouteTransition = "Fade");
        try
        {
            await TestHost.Services.GoToAsync("/a");
            await TestHost.Services.GoToAsync("/b", TestHost.With(RouteTransition.Slide));
            await TestHost.UntilAsync(() => region.Content is PageBView);

            Assert.IsTrue(region.ContentTransitions is { Count: 1 } transitions && transitions[0] is EntranceThemeTransition);
        }
        finally { await TestHost.UnloadAsync(region); }
    }

    [TestMethod]
    public async Task The_old_duration_of_the_region_plays_the_theme_transition()
    {
        var region = await LoadedRegionAsync(r => r.RouteTransitionDuration = TimeSpan.FromMilliseconds(200));
        try
        {
            await TestHost.Services.GoToAsync("/a");
            await TestHost.UntilAsync(() => region.Content is PageAView);

            Assert.IsNotNull(region.ContentTransitions);
        }
        finally { await TestHost.UnloadAsync(region); }
    }

    [TestMethod]
    public async Task None_on_the_navigation_stops_the_transition_of_the_region()
    {
        var region = await LoadedRegionAsync(r => r.RouteTransition = "Slide");
        try
        {
            await TestHost.Services.GoToAsync("/a");
            await TestHost.Services.GoToAsync("/b", TestHost.With(RouteTransition.None));
            await TestHost.UntilAsync(() => region.Content is PageBView);

            Assert.IsNull(region.ContentTransitions);
        }
        finally { await TestHost.UnloadAsync(region); }
    }

    [TestMethod]
    public async Task An_unknown_transition_name_shows_the_view_at_once_without_failing()
    {
        var region = await LoadedRegionAsync(r => r.RouteTransition = "NobodyKnowsThisOne");
        try
        {
            await TestHost.Services.GoToAsync("/a");
            await TestHost.UntilAsync(() => region.Content is PageAView);

            Assert.IsNull(region.ContentTransitions);
        }
        finally { await TestHost.UnloadAsync(region); }
    }

    [TestMethod]
    public async Task Rapid_changes_leave_the_region_on_the_last_view()
    {
        var region = await LoadedRegionAsync(r => r.RouteTransition = "Slide");
        try
        {
            for (var i = 0; i < 8; i++)
                await TestHost.Services.GoToAsync(i % 2 == 0 ? "/a" : "/b");

            await TestHost.UntilAsync(() => region.Content is PageBView);
        }
        finally { await TestHost.UnloadAsync(region); }
    }
}
