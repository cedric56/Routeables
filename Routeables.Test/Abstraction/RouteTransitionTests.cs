using Routeables.Abstraction;
using Routeables.Regions;
using Routeables.Routing;

namespace Routeables.Tests.Abstraction;

/// <summary>A transition is a name that each host plays; the navigation, then the region, decide which one, and which way the history moved.</summary>
public class RouteTransitionTests
{
    [Fact]
    public void A_built_in_name_gives_the_built_in_transition_whatever_its_case_and_a_blank_name_gives_none()
    {
        Assert.Same(RouteTransition.Slide, RouteTransition.Named("slide"));
        Assert.Same(RouteTransition.SlideUp, RouteTransition.Named(" SlideUp "));
        Assert.Same(RouteTransition.None, RouteTransition.Named(null));
        Assert.Same(RouteTransition.None, RouteTransition.Named(" "));
        Assert.Equal("Spin", RouteTransition.Named("Spin").Name);
        Assert.Same(RouteTransition.Fade, (RouteTransition)"Fade");
    }

    [Fact]
    public void The_arguments_carry_the_transition_of_the_navigation()
    {
        var args = RouteableArgs.Create().WithTransition(RouteTransition.Slide.WithDuration(TimeSpan.FromMilliseconds(400)));

        Assert.Equal("Slide", args.Transition!.Name);
        Assert.Equal(TimeSpan.FromMilliseconds(400), args.Transition.Duration);
        Assert.Null(args.WithTransition(null).Transition);
    }

    [Fact]
    public void The_navigation_wins_over_the_region_and_the_region_wins_over_the_old_duration()
    {
        var fromNavigation = new RegionChange(NavigationDirection.Forward, RouteTransition.Slide).Resolve(RouteTransition.Fade, TimeSpan.FromSeconds(1));
        Assert.Equal("Slide", fromNavigation.Transition.Name);
        Assert.True(fromNavigation.Declared);
        Assert.Equal(TimeSpan.FromSeconds(1), fromNavigation.Duration);

        var fromRegion = RegionChange.Unknown.Resolve(RouteTransition.Fade, TimeSpan.Zero);
        Assert.Equal("Fade", fromRegion.Transition.Name);
        Assert.Equal(RouteTransition.DefaultDuration, fromRegion.Duration);

        var legacy = RegionChange.Unknown.Resolve(null, TimeSpan.FromMilliseconds(220));
        Assert.Equal("Fade", legacy.Transition.Name);
        Assert.False(legacy.Declared);

        var nothing = RegionChange.Unknown.Resolve(null, TimeSpan.Zero);
        Assert.True(nothing.IsNone);
        Assert.False(nothing.Declared);

        Assert.True(new RegionChange(NavigationDirection.Forward, RouteTransition.None).Resolve(RouteTransition.Fade, TimeSpan.Zero).IsNone);
    }

    [Fact]
    public async Task The_host_is_told_which_way_the_history_moved_and_the_transition_of_the_navigation()
    {
        using var provider = RegionTestServices.Build(services => services.AddRegion("left"));
        var changes = new List<RegionChange>();
        using var host = new RegionBinding(provider.GetRouter("left"), (_, change) => changes.Add(change));
        Assert.Equal(NavigationDirection.None, changes[0].Direction);

        var cats = new RouteableEntry<RegionCatsViewModel>("/cats", null, "/cats", "/cats") { Region = "left" };
        cats.Arguments = RouteableArgs.Create().WithTransition(RouteTransition.Slide);
        await provider.GoToAsync(cats, TestContext.Current.CancellationToken);
        Assert.Equal(NavigationDirection.Forward, changes[1].Direction);
        Assert.Equal("Slide", changes[1].Transition!.Name);

        var dogs = new RouteableEntry<RegionDogsViewModel>("/dogs", null, "/dogs", "/dogs") { Region = "left" };
        await provider.GoToAsync(dogs, TestContext.Current.CancellationToken);
        Assert.Equal(NavigationDirection.Forward, changes[2].Direction);
        Assert.Null(changes[2].Transition);

        await provider.GetRouter("left").GoBackAsync(TestContext.Current.CancellationToken);
        Assert.Equal(NavigationDirection.Back, changes[3].Direction);
    }

    [Fact]
    public void The_built_in_set_has_fifteen_distinct_names_and_each_one_is_found_by_its_name()
    {
        Assert.Equal(15, RouteTransition.BuiltIn.Count);
        Assert.Equal(15, RouteTransition.BuiltIn.Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var transition in RouteTransition.BuiltIn)
        {
            Assert.Same(transition, RouteTransition.Named(transition.Name.ToLowerInvariant()));
            Assert.True(transition.IsBuiltIn);
        }

        Assert.False(RouteTransition.Named("Grow").IsBuiltIn);
        Assert.True(RouteTransition.Slide.WithDuration(TimeSpan.FromSeconds(1)).IsBuiltIn);
    }

    [Fact]
    public void None_and_a_custom_transition_have_no_start_state_and_every_other_one_has_one()
    {
        Assert.Null(RouteTransition.None.Start(NavigationDirection.Forward));
        Assert.Null(RouteTransition.Named("Grow").Start(NavigationDirection.Forward));

        foreach (var transition in RouteTransition.BuiltIn.Where(t => !t.IsNone))
            Assert.NotNull(transition.Start(NavigationDirection.Forward));
    }

    [Theory]
    [InlineData("Slide")]
    [InlineData("SlideFade")]
    [InlineData("Swipe")]
    public void A_slide_starts_on_the_side_the_history_moves_from(string name)
    {
        var forward = RouteTransition.Named(name).Start(NavigationDirection.Forward)!;
        var back = RouteTransition.Named(name).Start(NavigationDirection.Back)!;

        Assert.True(forward.TranslateX > 0);
        Assert.Equal(-forward.TranslateX, back.TranslateX);
    }

    [Fact]
    public void SlideUp_moves_vertically_and_Swipe_does_not_fade()
    {
        Assert.True(RouteTransition.SlideUp.Start(NavigationDirection.Forward)!.TranslateY > 0);
        Assert.True(RouteTransition.SlideUp.Start(NavigationDirection.Back)!.TranslateY < 0);
        Assert.Equal(1, RouteTransition.Swipe.Start(NavigationDirection.Forward)!.Opacity);
        Assert.Equal(0, RouteTransition.Fade.Start(NavigationDirection.Forward)!.Opacity);
    }

    [Fact]
    public void Pop_overshoots_and_Reveal_and_Curtain_grow_from_opposite_edges()
    {
        Assert.True(RouteTransition.Pop.Start(NavigationDirection.Forward)!.Overshoot);
        Assert.False(RouteTransition.Scale.Start(NavigationDirection.Forward)!.Overshoot);

        var reveal = RouteTransition.Reveal.Start(NavigationDirection.Forward)!;
        var curtain = RouteTransition.Curtain.Start(NavigationDirection.Forward)!;
        Assert.Equal(1, reveal.OriginY);
        Assert.Equal(0, curtain.OriginY);
        Assert.True(reveal.EffectiveScaleY < 0.1 && curtain.EffectiveScaleY < 0.1);
    }

    [Fact]
    public void Fade_only_changes_the_opacity()
        => Assert.False(RouteTransition.Fade.Start(NavigationDirection.Forward)!.Moves);

    [Fact]
    public void A_start_state_that_is_scaled_translated_or_turned_moves()
    {
        foreach (var name in new[] { "Slide", "SlideUp", "Pop", "Scale", "Lift", "Flip", "Rotate", "Reveal", "Curtain", "Spotlight", "Blur", "Swipe", "SlideFade" })
            Assert.True(RouteTransition.Named(name).Start(NavigationDirection.Forward)!.Moves, name);
    }
}
