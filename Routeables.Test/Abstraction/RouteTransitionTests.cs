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
}
