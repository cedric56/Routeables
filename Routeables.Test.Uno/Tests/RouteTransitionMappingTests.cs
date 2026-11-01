using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Routeables.Abstraction;
using Routeables.Regions;
using Routeables.Uno;

namespace Routeables.Test.Uno.Tests;

/// <summary>What <see cref="RouteTransitions"/> turns a transition into: the theme transitions the region plays. Pure mapping, no window needed.</summary>
[TestClass]
public class RouteTransitionMappingTests
{
    private static RegionTransition Of(string name, NavigationDirection direction = NavigationDirection.Forward, int ms = 250)
        => new(RouteTransition.Named(name), TimeSpan.FromMilliseconds(ms), direction, Declared: true);

    private static EntranceThemeTransition Entrance(TransitionCollection? transitions)
    {
        Assert.IsNotNull(transitions);
        Assert.AreEqual(1, transitions.Count);
        return (EntranceThemeTransition)transitions[0];
    }

    [TestMethod]
    public void None_has_no_transition()
        => Assert.IsNull(RouteTransitions.Create(Of("None"), 400, 300));

    [TestMethod]
    public void A_duration_of_zero_has_no_transition()
        => Assert.IsNull(RouteTransitions.Create(Of("Slide", ms: 0), 400, 300));

    [TestMethod]
    public void An_unknown_name_has_no_transition()
        => Assert.IsNull(RouteTransitions.Create(Of("NobodyKnowsThisOne"), 400, 300));

    [TestMethod]
    public void Fade_Blur_and_Spotlight_fade_the_content_in()
    {
        foreach (var name in new[] { "Fade", "Blur", "Spotlight" })
        {
            var transitions = RouteTransitions.Create(Of(name), 400, 300);
            Assert.IsNotNull(transitions, name);
            Assert.IsInstanceOfType(transitions[0], typeof(ContentThemeTransition), name);
        }
    }

    [TestMethod]
    public void Slide_comes_from_the_right_going_forward_and_from_the_left_going_back()
    {
        Assert.IsTrue(Entrance(RouteTransitions.Create(Of("Slide", NavigationDirection.Forward), 400, 300)).FromHorizontalOffset > 0);
        Assert.IsTrue(Entrance(RouteTransitions.Create(Of("Slide", NavigationDirection.Back), 400, 300)).FromHorizontalOffset < 0);
    }

    [TestMethod]
    public void Slide_with_no_direction_comes_like_going_forward()
        => Assert.IsTrue(Entrance(RouteTransitions.Create(Of("Slide", NavigationDirection.None), 400, 300)).FromHorizontalOffset > 0);

    [TestMethod]
    public void The_offset_grows_with_the_width_and_never_goes_under_forty()
    {
        var wide = Entrance(RouteTransitions.Create(Of("Slide"), 1000, 300)).FromHorizontalOffset;
        var narrow = Entrance(RouteTransitions.Create(Of("Slide"), 10, 300)).FromHorizontalOffset;

        Assert.IsTrue(wide > narrow);
        // Under forty the width no longer counts.
        Assert.AreEqual(narrow, Entrance(RouteTransitions.Create(Of("Slide"), 20, 300)).FromHorizontalOffset);
    }

    [TestMethod]
    public void SlideUp_comes_from_below()
        => Assert.IsTrue(Entrance(RouteTransitions.Create(Of("SlideUp"), 400, 300)).FromVerticalOffset > 0);

    [TestMethod]
    public void Every_built_in_transition_but_none_has_a_transition_in_both_directions()
    {
        foreach (var transition in RouteTransition.BuiltIn.Where(t => !t.IsNone))
        foreach (var direction in Enum.GetValues<NavigationDirection>())
            Assert.IsNotNull(RouteTransitions.Create(Of(transition.Name, direction), 400, 300), $"{transition.Name} {direction}");
    }

    [TestMethod]
    public void A_transition_that_moves_nowhere_but_resizes_rises_a_little()
    {
        foreach (var name in new[] { "Pop", "Scale", "Flip", "Rotate", "Reveal", "Curtain" })
        {
            var start = RouteTransition.Named(name).Start(NavigationDirection.Forward);
            if (start is { TranslateX: 0, TranslateY: 0, Moves: true })
                Assert.AreEqual(24, Entrance(RouteTransitions.Create(Of(name), 400, 300)).FromVerticalOffset, name);
        }
    }

    [TestMethod]
    public void A_custom_transition_is_asked_for_the_direction_and_wins_over_a_built_in_one()
    {
        NavigationDirection? seen = null;
        var made = new TransitionCollection { new ContentThemeTransition() };
        RouteTransitions.Register("MappingCustomSeen", direction => { seen = direction; return made; });

        var result = RouteTransitions.Create(Of("MappingCustomSeen", NavigationDirection.Back), 400, 300);

        Assert.AreSame(made, result);
        Assert.AreEqual(NavigationDirection.Back, seen);
    }

    [TestMethod]
    public void A_custom_name_is_found_whatever_its_case()
    {
        var made = new TransitionCollection();
        RouteTransitions.Register("MappingCustomCase", _ => made);

        Assert.AreSame(made, RouteTransitions.Create(Of("mappingcustomcase"), 400, 300));
    }

    [TestMethod]
    public void A_custom_transition_that_builds_nothing_has_no_transition()
    {
        RouteTransitions.Register("MappingCustomNull", _ => null);

        Assert.IsNull(RouteTransitions.Create(Of("MappingCustomNull"), 400, 300));
    }

    [TestMethod]
    public void Registering_a_transition_without_a_name_or_a_function_is_refused()
    {
        Refused<ArgumentException>(() => RouteTransitions.Register(" ", _ => null));
        Refused<ArgumentNullException>(() => RouteTransitions.Register("MappingCustomNoFunction", null!));
    }

    private static void Refused<TException>(Action action) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        Assert.Fail($"{typeof(TException).Name} was expected.");
    }
}
