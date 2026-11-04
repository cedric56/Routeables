using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Routeables.Abstraction;
using Routeables.Razor;
using Routeables.Routing;
using Routeables.Test.Abstraction;

namespace Routeables.Tests.Razor;

/// <summary>
/// The markup of a <see cref="RouteableRegion"/> for each way a transition can be asked: the classes and the duration the style sheet plays,
/// the direction of the history, the transition of the navigation over the one of the region, and no class at all when nothing is to play.
/// </summary>
public class RegionTransitionRenderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ------------------------------------------------------------------------------------------------ hosts: one region with one configuration each

    private abstract class RegionHost : ComponentBase
    {
        protected abstract void Configure(RenderTreeBuilder builder);

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<RouteableRegion>(0);
            Configure(builder);
            builder.CloseComponent();
        }
    }

    private sealed class PlainHost : RegionHost
    {
        protected override void Configure(RenderTreeBuilder builder) { }
    }

    private sealed class SlideHost : RegionHost
    {
        protected override void Configure(RenderTreeBuilder builder)
            => builder.AddAttribute(1, nameof(RouteableRegion.Transition), RouteTransition.Slide);
    }

    private sealed class SlowSlideHost : RegionHost
    {
        protected override void Configure(RenderTreeBuilder builder)
            => builder.AddAttribute(1, nameof(RouteableRegion.Transition), RouteTransition.Slide.WithDuration(TimeSpan.FromMilliseconds(900)));
    }

    private sealed class NoneHost : RegionHost
    {
        protected override void Configure(RenderTreeBuilder builder)
            => builder.AddAttribute(1, nameof(RouteableRegion.Transition), RouteTransition.None);
    }

    private sealed class CustomHost : RegionHost
    {
        protected override void Configure(RenderTreeBuilder builder)
            => builder.AddAttribute(1, nameof(RouteableRegion.Transition), RouteTransition.Named("Grow It!"));
    }

    private sealed class LegacyHost : RegionHost
    {
        protected override void Configure(RenderTreeBuilder builder)
        {
            builder.AddAttribute(1, nameof(RouteableRegion.TransitionDuration), TimeSpan.FromMilliseconds(400));
            builder.AddAttribute(2, nameof(RouteableRegion.Class), "frame");
        }
    }

    private sealed class ClassHost : RegionHost
    {
        protected override void Configure(RenderTreeBuilder builder)
            => builder.AddAttribute(1, nameof(RouteableRegion.Class), "frame");
    }

    private sealed class ClassAndSlideHost : RegionHost
    {
        protected override void Configure(RenderTreeBuilder builder)
        {
            builder.AddAttribute(1, nameof(RouteableRegion.Class), "frame");
            builder.AddAttribute(2, nameof(RouteableRegion.Transition), RouteTransition.Slide);
        }
    }

    private sealed class LeftHost : RegionHost
    {
        protected override void Configure(RenderTreeBuilder builder)
        {
            builder.AddAttribute(1, nameof(RouteableRegion.Region), "left");
            builder.AddAttribute(2, nameof(RouteableRegion.Transition), RouteTransition.Fade);
        }
    }

    // ------------------------------------------------------------------------------------------------ helpers

    private static void Register(IServiceCollection services)
    {
        services.RegisterRouting(ServiceLifetime.Scoped);
        services.AddRegion("left");
    }

    private static IRouter DefaultRouter<T>(RazorHostHarness<T> harness) where T : IComponent
        => harness.Get<IRouter>();

    private static IRouter LeftRouter<T>(RazorHostHarness<T> harness) where T : IComponent
        => harness.Get<IServiceProvider>().GetRouter("left");

    private static async Task<RouteableResult> PushAsync(IRouter router, string name, IRouteableArgs? args = null)
    {
        var log = new CallLog();
        return await router.GoAsync(new RecordingView($"view{name}", log), new RecordingViewModel($"vm{name}", log), args, Ct);
    }

    private static IRouteableArgs With(RouteTransition transition) => RouteableArgs.Create().WithTransition(transition);

    /// <summary>The markup, once it satisfies <paramref name="expected"/> (the region redraws on the renderer, not on the thread of the test).</summary>
    private static async Task<string> HtmlWhenAsync<T>(RazorHostHarness<T> harness, Func<string, bool> expected) where T : IComponent
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            var html = await harness.HtmlAsync();
            if (expected(html)) return html;
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"The markup never became what was expected. Last markup: {html}");
            await Task.Delay(10, Ct);
        }
    }

    // ------------------------------------------------------------------------------------------------ nothing to play

    [Fact]
    public async Task An_empty_region_is_a_plain_element()
    {
        await using var harness = await RazorHostHarness<SlideHost>.StartAsync(Register);

        Assert.Equal("<div></div>", await harness.HtmlAsync());
    }

    [Fact]
    public async Task A_region_with_no_transition_gets_no_class_and_no_style_when_a_page_comes_in()
    {
        await using var harness = await RazorHostHarness<PlainHost>.StartAsync(Register);

        await PushAsync(DefaultRouter(harness), "A");
        await PushAsync(DefaultRouter(harness), "B");

        var html = await harness.HtmlAsync();
        Assert.DoesNotContain("rt-enter", html);
        Assert.DoesNotContain("--rt-duration", html);
    }

    [Fact]
    public async Task A_region_that_declares_none_plays_nothing()
    {
        await using var harness = await RazorHostHarness<NoneHost>.StartAsync(Register);

        await PushAsync(DefaultRouter(harness), "A");

        Assert.DoesNotContain("rt-enter", await harness.HtmlAsync());
    }

    [Fact]
    public async Task A_navigation_that_asks_for_none_stops_the_transition_of_the_region()
    {
        await using var harness = await RazorHostHarness<SlideHost>.StartAsync(Register);
        var router = DefaultRouter(harness);
        await PushAsync(router, "A");
        await HtmlWhenAsync(harness, h => h.Contains("rt-slide"));

        await PushAsync(router, "B", With(RouteTransition.None));

        await HtmlWhenAsync(harness, h => !h.Contains("rt-enter"));
    }

    [Fact]
    public async Task A_transition_whose_own_duration_is_zero_plays_nothing()
    {
        await using var harness = await RazorHostHarness<SlideHost>.StartAsync(Register);

        await PushAsync(DefaultRouter(harness), "A", With(RouteTransition.Fade.WithDuration(TimeSpan.Zero)));

        await HtmlWhenAsync(harness, h => !h.Contains("rt-enter"));
    }

    [Fact]
    public async Task Erasing_the_history_empties_the_region_and_the_classes_go_with_the_page()
    {
        await using var harness = await RazorHostHarness<SlideHost>.StartAsync(Register);
        var router = DefaultRouter(harness);
        await PushAsync(router, "A");
        await HtmlWhenAsync(harness, h => h.Contains("rt-enter"));

        await router.EraseHistoryAsync(Ct);

        await HtmlWhenAsync(harness, h => h == "<div></div>");
    }

    // ------------------------------------------------------------------------------------------------ what is played

    [Fact]
    public async Task A_page_that_comes_in_forward_carries_the_classes_and_the_default_duration_of_the_transition_of_the_region()
    {
        await using var harness = await RazorHostHarness<SlideHost>.StartAsync(Register);

        await PushAsync(DefaultRouter(harness), "A");

        var html = await HtmlWhenAsync(harness, h => h.Contains("rt-enter"));
        Assert.Contains("rt-enter rt-slide rt-forward", html);
        Assert.Contains("--rt-duration:250ms", html);
    }

    [Fact]
    public async Task Going_back_plays_the_back_variant_and_going_forward_again_the_forward_one()
    {
        await using var harness = await RazorHostHarness<SlideHost>.StartAsync(Register);
        var router = DefaultRouter(harness);
        await PushAsync(router, "A");
        await PushAsync(router, "B");

        await router.GoBackAsync(Ct);
        await HtmlWhenAsync(harness, h => h.Contains("rt-back"));

        await router.ForwardAsync(Ct);
        await HtmlWhenAsync(harness, h => h.Contains("rt-forward") && !h.Contains("rt-back"));
    }

    [Fact]
    public async Task Pop_and_going_back_to_the_root_play_the_back_variant()
    {
        await using var harness = await RazorHostHarness<SlideHost>.StartAsync(Register);
        var router = DefaultRouter(harness);
        await PushAsync(router, "A");
        await PushAsync(router, "B");
        await PushAsync(router, "C");

        await router.PopAsync(Ct);
        await HtmlWhenAsync(harness, h => h.Contains("rt-back"));

        await PushAsync(router, "D");
        await HtmlWhenAsync(harness, h => h.Contains("rt-forward"));

        await router.GoBackToRootAsync(Ct);
        await HtmlWhenAsync(harness, h => h.Contains("rt-back"));
    }

    [Fact]
    public async Task MoveTo_plays_the_direction_of_the_move()
    {
        await using var harness = await RazorHostHarness<SlideHost>.StartAsync(Register);
        var router = DefaultRouter(harness);
        await PushAsync(router, "A");
        await PushAsync(router, "B");
        await PushAsync(router, "C");

        await router.MoveToAsync(0, Ct);
        await HtmlWhenAsync(harness, h => h.Contains("rt-back"));

        await router.MoveToAsync(2, Ct);
        await HtmlWhenAsync(harness, h => h.Contains("rt-forward"));
    }

    [Fact]
    public async Task The_transition_of_the_navigation_wins_over_the_one_of_the_region()
    {
        await using var harness = await RazorHostHarness<SlideHost>.StartAsync(Register);

        await PushAsync(DefaultRouter(harness), "A", With(RouteTransition.Fade));

        var html = await HtmlWhenAsync(harness, h => h.Contains("rt-enter"));
        Assert.Contains("rt-fade", html);
        Assert.DoesNotContain("rt-slide", html);
    }

    [Fact]
    public async Task The_transition_of_a_navigation_is_not_kept_for_the_next_page()
    {
        await using var harness = await RazorHostHarness<SlideHost>.StartAsync(Register);
        var router = DefaultRouter(harness);
        await PushAsync(router, "A", With(RouteTransition.Fade));
        await HtmlWhenAsync(harness, h => h.Contains("rt-fade"));

        await PushAsync(router, "B");

        var html = await HtmlWhenAsync(harness, h => h.Contains("rt-slide"));
        Assert.DoesNotContain("rt-fade", html);
    }

    [Fact]
    public async Task The_duration_of_the_transition_of_the_region_is_the_one_in_the_style()
    {
        await using var harness = await RazorHostHarness<SlowSlideHost>.StartAsync(Register);

        await PushAsync(DefaultRouter(harness), "A");

        Assert.Contains("--rt-duration:900ms", await HtmlWhenAsync(harness, h => h.Contains("rt-enter")));
    }

    [Fact]
    public async Task The_duration_of_the_transition_of_the_navigation_wins_over_the_default()
    {
        await using var harness = await RazorHostHarness<SlideHost>.StartAsync(Register);

        await PushAsync(DefaultRouter(harness), "A", With(RouteTransition.Slide.WithDuration(TimeSpan.FromMilliseconds(1200))));

        Assert.Contains("--rt-duration:1200ms", await HtmlWhenAsync(harness, h => h.Contains("rt-enter")));
    }

    [Fact]
    public async Task The_old_duration_of_the_region_is_a_fade_of_that_duration_and_the_class_of_the_region_comes_first()
    {
        await using var harness = await RazorHostHarness<LegacyHost>.StartAsync(Register);

        await PushAsync(DefaultRouter(harness), "A");

        var html = await HtmlWhenAsync(harness, h => h.Contains("rt-enter"));
        Assert.Contains("class=\"frame rt-enter rt-fade rt-forward\"", html);
        Assert.Contains("--rt-duration:400ms", html);
    }

    [Fact]
    public async Task The_class_of_the_region_is_on_the_element_with_or_without_a_page()
    {
        await using var harness = await RazorHostHarness<ClassHost>.StartAsync(Register);
        Assert.Equal("<div class=\"frame\"></div>", await harness.HtmlAsync());

        await PushAsync(DefaultRouter(harness), "A");

        Assert.Equal("<div class=\"frame\"></div>", await harness.HtmlAsync());
    }

    [Fact]
    public async Task The_class_of_the_region_stays_with_the_transition_classes()
    {
        await using var harness = await RazorHostHarness<ClassAndSlideHost>.StartAsync(Register);

        await PushAsync(DefaultRouter(harness), "A");

        Assert.Contains("class=\"frame rt-enter rt-slide rt-forward\"", await HtmlWhenAsync(harness, h => h.Contains("rt-enter")));
    }

    [Fact]
    public async Task A_custom_name_becomes_a_safe_css_class()
    {
        await using var harness = await RazorHostHarness<CustomHost>.StartAsync(Register);

        await PushAsync(DefaultRouter(harness), "A");

        // Lower case, and every character that is not a letter, a digit, a dash or an underscore becomes a dash.
        Assert.Contains("rt-enter rt-grow-it- rt-forward", await HtmlWhenAsync(harness, h => h.Contains("rt-enter")));
    }

    [Theory]
    [MemberData(nameof(BuiltInNames))]
    public async Task Every_built_in_transition_gives_its_own_class(string name)
    {
        await using var harness = await RazorHostHarness<SlideHost>.StartAsync(Register);

        await PushAsync(DefaultRouter(harness), "A", With(RouteTransition.Named(name)));

        if (name == "None")
            await HtmlWhenAsync(harness, h => !h.Contains("rt-enter"));
        else
            Assert.Contains($"rt-enter rt-{name.ToLowerInvariant()} rt-forward", await HtmlWhenAsync(harness, h => h.Contains("rt-enter")));
    }

    public static TheoryData<string> BuiltInNames()
    {
        var data = new TheoryData<string>();
        foreach (var transition in RouteTransition.BuiltIn)
            data.Add(transition.Name);
        return data;
    }

    // ------------------------------------------------------------------------------------------------ regions

    [Fact]
    public async Task A_region_with_a_name_follows_its_own_router_only()
    {
        await using var harness = await RazorHostHarness<LeftHost>.StartAsync(Register);

        await PushAsync(DefaultRouter(harness), "OnDefault");
        await Task.Delay(100, Ct);
        Assert.Equal("<div></div>", await harness.HtmlAsync());

        await PushAsync(LeftRouter(harness), "OnLeft");
        var html = await HtmlWhenAsync(harness, h => h.Contains("rt-enter"));
        Assert.Contains("rt-enter rt-fade rt-forward", html);
    }

    [Fact]
    public async Task A_refused_navigation_changes_nothing_in_the_markup()
    {
        await using var harness = await RazorHostHarness<SlideHost>.StartAsync(Register);
        var router = DefaultRouter(harness);
        var log = new CallLog();
        var first = new RecordingViewModel("first", log);
        await router.GoAsync(new RecordingView("firstView", log), first, null, Ct);
        var before = await HtmlWhenAsync(harness, h => h.Contains("rt-enter"));
        first.CanLeave = false;

        var result = await PushAsync(router, "B", With(RouteTransition.Fade));
        await Task.Delay(100, Ct);

        Assert.False(result.Success);
        Assert.Equal(before, await harness.HtmlAsync());
    }

    [Fact]
    public async Task Pages_pushed_in_a_row_leave_one_element_with_the_classes_of_the_last_one()
    {
        await using var harness = await RazorHostHarness<SlideHost>.StartAsync(Register);
        var router = DefaultRouter(harness);

        for (var i = 0; i < 10; i++)
            await PushAsync(router, $"P{i}");

        var html = await HtmlWhenAsync(harness, h => h.Contains("rt-enter"));
        Assert.Equal(1, CountOf(html, "<div"));
        Assert.Equal(1, CountOf(html, "rt-enter"));
        Assert.Contains("rt-forward", html);
    }

    private static int CountOf(string text, string part)
    {
        var count = 0;
        for (var index = text.IndexOf(part, StringComparison.Ordinal); index >= 0; index = text.IndexOf(part, index + part.Length, StringComparison.Ordinal))
            count++;
        return count;
    }
}
