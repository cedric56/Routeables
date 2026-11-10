using Microsoft.UI.Xaml.Media.Animation;
using Routeables.Abstraction;
using Routeables.Abstraction.Transitions;
using Routeables.Regions;
using Routeables.Uno;

namespace Routeables.Test.Uno.Tests;

/// <summary>What <see cref="RouteTransitions"/> turns a transition into: the theme transitions the region plays. Pure mapping, no window needed.</summary>
[TestClass]
public class RouteTransitionMappingTests
{
    /// <summary>Builds a <see cref="RegionTransition"/> for the given name, direction and duration.</summary>
    private static RegionTransition Of(string name, RouteableDirection direction = RouteableDirection.Forward, int ms = 250)
        => new(RouteTransition.Named(name), TimeSpan.FromMilliseconds(ms), direction, Declared: true);

    /// <summary>Asserts that the collection contains a single <see cref="EntranceThemeTransition"/> and returns it.</summary>
    private static EntranceThemeTransition Entrance(TransitionCollection? transitions)
    {
        Assert.IsNotNull(transitions);
        Assert.AreEqual(1, transitions.Count);
        return (EntranceThemeTransition)transitions[0];
    }

    /// <summary>None produces no transition.</summary>
    [TestMethod]
    public void None_has_no_transition()
        => Assert.IsNull(RouteTransitions.Create(Of("None"), 400, 300));

    /// <summary>A duration of zero produces no transition.</summary>
    [TestMethod]
    public void A_duration_of_zero_has_no_transition()
        => Assert.IsNull(RouteTransitions.Create(Of("Slide", ms: 0), 400, 300));

    /// <summary>An unknown name produces no transition.</summary>
    [TestMethod]
    public void An_unknown_name_has_no_transition()
        => Assert.IsNull(RouteTransitions.Create(Of("NobodyKnowsThisOne"), 400, 300));

    /// <summary>Fade, Blur and Spotlight fade the content in with a content theme transition.</summary>
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

    /// <summary>Slide comes from the right going forward and from the left going back.</summary>
    [TestMethod]
    public void Slide_comes_from_the_right_going_forward_and_from_the_left_going_back()
    {
        Assert.IsTrue(Entrance(RouteTransitions.Create(Of("Slide", RouteableDirection.Forward), 400, 300)).FromHorizontalOffset > 0);
        Assert.IsTrue(Entrance(RouteTransitions.Create(Of("Slide", RouteableDirection.Back), 400, 300)).FromHorizontalOffset < 0);
    }

    /// <summary>Slide with no direction behaves like going forward.</summary>
    [TestMethod]
    public void Slide_with_no_direction_comes_like_going_forward()
        => Assert.IsTrue(Entrance(RouteTransitions.Create(Of("Slide", RouteableDirection.None), 400, 300)).FromHorizontalOffset > 0);

    /// <summary>The horizontal offset grows with the width and never goes under forty.</summary>
    [TestMethod]
    public void The_offset_grows_with_the_width_and_never_goes_under_forty()
    {
        var wide = Entrance(RouteTransitions.Create(Of("Slide"), 1000, 300)).FromHorizontalOffset;
        var narrow = Entrance(RouteTransitions.Create(Of("Slide"), 10, 300)).FromHorizontalOffset;

        Assert.IsTrue(wide > narrow);
        // Under forty the width no longer counts.
        Assert.AreEqual(narrow, Entrance(RouteTransitions.Create(Of("Slide"), 20, 300)).FromHorizontalOffset);
    }

    /// <summary>SlideUp comes from below.</summary>
    [TestMethod]
    public void SlideUp_comes_from_below()
        => Assert.IsTrue(Entrance(RouteTransitions.Create(Of("SlideUp"), 400, 300)).FromVerticalOffset > 0);

    /// <summary>Every built-in transition except None produces a transition in both directions.</summary>
    [TestMethod]
    public void Every_built_in_transition_but_none_has_a_transition_in_both_directions()
    {
        foreach (var transition in RouteTransition.BuiltIn.Where(t => !t.IsNone))
            foreach (var direction in Enum.GetValues<RouteableDirection>())
                Assert.IsNotNull(RouteTransitions.Create(Of(transition.Name, direction), 400, 300), $"{transition.Name} {direction}");
    }

    /// <summary>A transition that moves nowhere but resizes rises a little (24 logical pixels).</summary>
    [TestMethod]
    public void A_transition_that_moves_nowhere_but_resizes_rises_a_little()
    {
        foreach (var name in new[] { "Pop", "Scale", "Flip", "Rotate", "Reveal", "Curtain" })
        {
            var start = RouteTransition.Named(name).Start(RouteableDirection.Forward);
            if (start is { TranslateX: 0, TranslateY: 0, Moves: true })
                Assert.AreEqual(24, Entrance(RouteTransitions.Create(Of(name), 400, 300)).FromVerticalOffset, name);
        }
    }

    /// <summary>A custom transition is asked for the direction and wins over a built-in one of the same name.</summary>
    [TestMethod]
    public void A_custom_transition_is_asked_for_the_direction_and_wins_over_a_built_in_one()
    {
        RouteableDirection? seen = null;
        var made = new TransitionCollection { new ContentThemeTransition() };
        RouteTransitions.Register("MappingCustomSeen", direction => { seen = direction; return made; });

        var result = RouteTransitions.Create(Of("MappingCustomSeen", RouteableDirection.Back), 400, 300);

        Assert.AreSame(made, result);
        Assert.AreEqual(RouteableDirection.Back, seen);
    }

    /// <summary>A custom transition name is resolved case-insensitively.</summary>
    [TestMethod]
    public void A_custom_name_is_found_whatever_its_case()
    {
        var made = new TransitionCollection();
        RouteTransitions.Register("MappingCustomCase", _ => made);

        Assert.AreSame(made, RouteTransitions.Create(Of("mappingcustomcase"), 400, 300));
    }

    /// <summary>A custom transition whose factory returns null produces no transition.</summary>
    [TestMethod]
    public void A_custom_transition_that_builds_nothing_has_no_transition()
    {
        RouteTransitions.Register("MappingCustomNull", _ => null);

        Assert.IsNull(RouteTransitions.Create(Of("MappingCustomNull"), 400, 300));
    }

    /// <summary>Registering a transition without a valid name or without a factory is refused.</summary>
    [TestMethod]
    public void Registering_a_transition_without_a_name_or_a_function_is_refused()
    {
        Refused<ArgumentException>(() => RouteTransitions.Register(" ", _ => null));
        Refused<ArgumentNullException>(() => RouteTransitions.Register("MappingCustomNoFunction", null!));
    }

    /// <summary>Asserts that <paramref name="action"/> throws an exception of type <typeparamref name="TException"/>.</summary>
    private static void Refused<TException>(Action action) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        Assert.Fail($"{typeof(TException).Name} was expected.");
    }
}