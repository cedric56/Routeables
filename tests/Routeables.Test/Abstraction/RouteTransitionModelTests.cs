using Routeables.Abstraction;
using Routeables.Abstraction.Transitions;
using Routeables.Regions;

namespace Routeables.Tests.Abstraction;

/// <summary>
/// The model of a transition, with no host: how a name is read, compared and shown, the state each built-in transition starts from, and how
/// the navigation, the region and the legacy duration are put together. Every host plays what these say, so a change here is a change everywhere.
/// </summary>
public class RouteTransitionModelTests
{
    // ------------------------------------------------------------------------------------------------ names

    [Theory]
    [InlineData("Fade", "Fade")]
    [InlineData("fade", "Fade")]
    [InlineData("FADE", "Fade")]
    [InlineData("  Fade\t", "Fade")]
    [InlineData("slideup", "SlideUp")]
    [InlineData("SLIDEFADE", "SlideFade")]
    public void A_built_in_name_is_found_whatever_its_case_and_its_blanks(string name, string expected)
        => Assert.Equal(expected, RouteTransition.Named(name).Name);

    [Theory]
    [InlineData("Grow")]
    [InlineData("  Grow  ")]
    [InlineData("grow")]
    public void A_custom_name_is_kept_trimmed_and_as_written(string name)
    {
        var transition = RouteTransition.Named(name);

        Assert.Equal(name.Trim(), transition.Name);
        Assert.False(transition.IsBuiltIn);
        Assert.False(transition.IsNone);
        Assert.Null(transition.Duration);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("none")]
    [InlineData("NONE")]
    public void No_name_or_none_is_the_none_transition(string? name)
    {
        var transition = RouteTransition.Named(name);

        Assert.Same(RouteTransition.None, transition);
        Assert.True(transition.IsNone);
    }

    [Fact]
    public void A_string_converts_to_the_transition_of_that_name()
    {
        RouteTransition fade = "fade";
        RouteTransition custom = "Grow";

        Assert.Same(RouteTransition.Fade, fade);
        Assert.Equal("Grow", custom.Name);
    }

    [Fact]
    public void A_null_string_converts_to_none()
    {
        string? name = null;

        RouteTransition transition = name!;

        Assert.Same(RouteTransition.None, transition);
    }

    // ------------------------------------------------------------------------------------------------ duration

    [Fact]
    public void The_default_duration_is_a_quarter_of_a_second()
        => Assert.Equal(TimeSpan.FromMilliseconds(250), RouteTransition.DefaultDuration);

    [Fact]
    public void A_built_in_transition_has_no_duration_of_its_own()
        => Assert.All(RouteTransition.BuiltIn, t => Assert.Null(t.Duration));

    [Fact]
    public void WithDuration_gives_a_new_transition_of_the_same_name_and_leaves_the_original_alone()
    {
        var slow = RouteTransition.Slide.WithDuration(TimeSpan.FromSeconds(2));

        Assert.NotSame(RouteTransition.Slide, slow);
        Assert.Equal("Slide", slow.Name);
        Assert.Equal(TimeSpan.FromSeconds(2), slow.Duration);
        Assert.Null(RouteTransition.Slide.Duration);
    }

    [Fact]
    public void WithDuration_can_be_applied_again_and_the_last_one_wins()
    {
        var transition = RouteTransition.Fade.WithDuration(TimeSpan.FromSeconds(1)).WithDuration(TimeSpan.FromMilliseconds(100));

        Assert.Equal(TimeSpan.FromMilliseconds(100), transition.Duration);
    }

    [Fact]
    public void A_custom_transition_can_have_a_duration()
    {
        var transition = RouteTransition.Named("Grow").WithDuration(TimeSpan.FromMilliseconds(300));

        Assert.Equal("Grow", transition.Name);
        Assert.Equal(TimeSpan.FromMilliseconds(300), transition.Duration);
        Assert.False(transition.IsBuiltIn);
    }

    [Fact]
    public void None_with_a_duration_is_still_none()
        => Assert.True(RouteTransition.None.WithDuration(TimeSpan.FromSeconds(1)).IsNone);

    // ------------------------------------------------------------------------------------------------ equality

    [Fact]
    public void Two_transitions_are_equal_when_their_names_match_whatever_the_case_and_their_durations_match()
    {
        var a = RouteTransition.Named("Grow");
        var b = RouteTransition.Named("GROW");

        Assert.Equal(a, b);
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void A_different_duration_makes_a_different_transition()
    {
        var a = RouteTransition.Fade.WithDuration(TimeSpan.FromMilliseconds(100));
        var b = RouteTransition.Fade.WithDuration(TimeSpan.FromMilliseconds(200));

        Assert.NotEqual(a, b);
        Assert.NotEqual(RouteTransition.Fade, a);
    }

    [Fact]
    public void A_transition_is_never_equal_to_null_or_to_another_type()
    {
        Assert.False(RouteTransition.Fade.Equals(null));
        Assert.False(RouteTransition.Fade.Equals((object?)"Fade"));
    }

    [Fact]
    public void A_transition_can_be_a_dictionary_key_whatever_the_case()
    {
        var map = new Dictionary<RouteTransition, int> { [RouteTransition.Named("Grow")] = 1 };

        Assert.Equal(1, map[RouteTransition.Named("grow")]);
        Assert.False(map.ContainsKey(RouteTransition.Named("grow").WithDuration(TimeSpan.FromSeconds(1))));
    }

    [Fact]
    public void ToString_shows_the_name_and_the_duration_in_milliseconds_when_there_is_one()
    {
        Assert.Equal("Fade", RouteTransition.Fade.ToString());
        Assert.Equal("Slide (400 ms)", RouteTransition.Slide.WithDuration(TimeSpan.FromMilliseconds(400)).ToString());
        Assert.Equal("Grow", RouteTransition.Named("Grow").ToString());
    }

    // ------------------------------------------------------------------------------------------------ the built-in set

    [Fact]
    public void The_built_in_list_starts_with_none_and_has_no_null()
    {
        Assert.Same(RouteTransition.None, RouteTransition.BuiltIn[0]);
        Assert.All(RouteTransition.BuiltIn, Assert.NotNull);
    }

    [Fact]
    public void Every_property_of_the_built_in_set_is_in_the_list()
    {
        var properties = typeof(RouteTransition).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(RouteTransition))
            .Select(p => (RouteTransition)p.GetValue(null)!)
            .ToList();

        // A transition added as a property but forgotten in BuiltIn would not be found by name.
        Assert.Equal(properties.Count, RouteTransition.BuiltIn.Count);
        Assert.All(properties, p => Assert.Contains(p, RouteTransition.BuiltIn));
    }

    // ------------------------------------------------------------------------------------------------ the start state

    [Theory]
    [InlineData(RouteableDirection.Forward)]
    [InlineData(RouteableDirection.Back)]
    [InlineData(RouteableDirection.None)]
    public void The_start_state_of_a_built_in_transition_is_never_the_rest_state_unless_the_transition_is_none(RouteableDirection direction)
    {
        foreach (var transition in RouteTransition.BuiltIn.Where(t => !t.IsNone))
        {
            var start = transition.Start(direction);
            Assert.NotNull(start);
            Assert.NotEqual(RouteTransitionStart.Rest, start);
        }
    }

    [Fact]
    public void A_direction_of_none_starts_like_a_forward_move()
    {
        foreach (var transition in RouteTransition.BuiltIn.Where(t => !t.IsNone))
            Assert.Equal(transition.Start(RouteableDirection.Forward), transition.Start(RouteableDirection.None));
    }

    [Fact]
    public void The_start_state_does_not_depend_on_the_case_of_the_name_given_to_Named()
    {
        foreach (var transition in RouteTransition.BuiltIn.Where(t => !t.IsNone))
        {
            var lower = RouteTransition.Named(transition.Name.ToLowerInvariant());
            Assert.Equal(transition.Start(RouteableDirection.Forward), lower.Start(RouteableDirection.Forward));
        }
    }

    [Fact]
    public void A_duration_does_not_change_the_start_state()
    {
        foreach (var transition in RouteTransition.BuiltIn.Where(t => !t.IsNone))
            Assert.Equal(
                transition.Start(RouteableDirection.Forward),
                transition.WithDuration(TimeSpan.FromSeconds(3)).Start(RouteableDirection.Forward));
    }

    [Fact]
    public void Only_the_slides_depend_on_the_direction()
    {
        var directional = RouteTransition.BuiltIn
            .Where(t => !t.IsNone && !Equals(t.Start(RouteableDirection.Forward), t.Start(RouteableDirection.Back)))
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Slide", "SlideFade", "SlideUp", "Swipe"], directional);
    }

    [Theory]
    [InlineData("Fade", 0.0, 1.0, 1.0, 0.0, 0.0, 0.0)]
    [InlineData("Scale", 0.0, 0.8, 0.8, 0.0, 0.0, 0.0)]
    [InlineData("Pop", 0.0, 0.85, 0.85, 0.0, 0.0, 0.0)]
    [InlineData("Blur", 0.0, 1.03, 1.03, 0.0, 0.0, 0.0)]
    [InlineData("Spotlight", 0.0, 1.06, 1.06, 0.0, 0.0, 0.0)]
    [InlineData("Rotate", 0.0, 0.7, 0.7, 0.0, 0.0, -90.0)]
    [InlineData("Flip", 0.0, 1.0, 0.02, 0.0, 0.0, 0.0)]
    public void The_start_state_of_a_forward_transition_is_the_one_documented(string name, double opacity, double scale, double effectiveX, double translateX, double translateY, double rotate)
    {
        var start = RouteTransition.Named(name).Start(RouteableDirection.Forward)!;

        Assert.Equal(opacity, start.Opacity);
        Assert.Equal(scale, start.Scale, 3);
        Assert.Equal(effectiveX, start.EffectiveScaleX, 3);
        Assert.Equal(translateX, start.TranslateX);
        Assert.Equal(translateY, start.TranslateY);
        Assert.Equal(rotate, start.Rotate);
    }

    [Fact]
    public void Lift_moves_down_a_little_and_shrinks_a_little()
    {
        var start = RouteTransition.Lift.Start(RouteableDirection.Forward)!;

        Assert.True(start.TranslateY > 0 && start.TranslateY < 0.1);
        Assert.True(start.Scale < 1 && start.Scale > 0.9);
    }

    [Fact]
    public void Blur_and_Spotlight_start_larger_than_rest_and_the_others_that_scale_start_smaller()
    {
        Assert.True(RouteTransition.Blur.Start(RouteableDirection.Forward)!.Scale > 1);
        Assert.True(RouteTransition.Spotlight.Start(RouteableDirection.Forward)!.Scale > 1);
        foreach (var smaller in new[] { RouteTransition.Pop, RouteTransition.Scale, RouteTransition.Lift, RouteTransition.Rotate })
            Assert.True(smaller.Start(RouteableDirection.Forward)!.Scale < 1, smaller.Name);
    }

    [Fact]
    public void The_transitions_that_slide_in_from_nothing_start_at_full_opacity()
    {
        foreach (var name in new[] { "Swipe", "Reveal", "Curtain" })
            Assert.Equal(1, RouteTransition.Named(name).Start(RouteableDirection.Forward)!.Opacity);

        foreach (var name in new[] { "Fade", "Slide", "SlideUp", "SlideFade", "Pop", "Scale", "Lift", "Blur", "Spotlight", "Flip", "Rotate" })
            Assert.Equal(0, RouteTransition.Named(name).Start(RouteableDirection.Forward)!.Opacity);
    }

    [Fact]
    public void Every_start_state_has_an_origin_inside_the_view()
    {
        foreach (var transition in RouteTransition.BuiltIn.Where(t => !t.IsNone))
        {
            var start = transition.Start(RouteableDirection.Forward)!;
            Assert.InRange(start.OriginX, 0, 1);
            Assert.InRange(start.OriginY, 0, 1);
        }
    }

    [Fact]
    public void Every_start_state_has_a_positive_scale_so_it_can_be_interpolated()
    {
        foreach (var transition in RouteTransition.BuiltIn.Where(t => !t.IsNone))
        {
            var start = transition.Start(RouteableDirection.Forward)!;
            Assert.True(start.EffectiveScaleX > 0 && start.EffectiveScaleY > 0, transition.Name);
        }
    }

    [Fact]
    public void A_translation_is_a_fraction_of_the_size_of_the_view_never_more_than_a_whole_view()
    {
        foreach (var transition in RouteTransition.BuiltIn.Where(t => !t.IsNone))
            foreach (var direction in new[] { RouteableDirection.Forward, RouteableDirection.Back })
            {
                var start = transition.Start(direction)!;
                Assert.InRange(start.TranslateX, -1, 1);
                Assert.InRange(start.TranslateY, -1, 1);
            }
    }

    // ------------------------------------------------------------------------------------------------ RouteTransitionStart

    [Fact]
    public void The_rest_state_is_fully_opaque_and_does_not_move()
    {
        var rest = RouteTransitionStart.Rest;

        Assert.Equal(1, rest.Opacity);
        Assert.Equal(1, rest.EffectiveScaleX);
        Assert.Equal(1, rest.EffectiveScaleY);
        Assert.False(rest.Moves);
        Assert.False(rest.Overshoot);
        Assert.Equal(0.5, rest.OriginX);
        Assert.Equal(0.5, rest.OriginY);
    }

    [Fact]
    public void A_new_start_state_is_transparent_and_at_rest_otherwise()
    {
        var start = new RouteTransitionStart();

        Assert.Equal(0, start.Opacity);
        Assert.False(start.Moves);
    }

    [Fact]
    public void The_effective_scale_is_the_overall_scale_times_the_scale_of_the_axis()
    {
        var start = new RouteTransitionStart { Scale = 0.5, ScaleX = 0.5, ScaleY = 2 };

        Assert.Equal(0.25, start.EffectiveScaleX, 6);
        Assert.Equal(1.0, start.EffectiveScaleY, 6);
    }

    [Theory]
    [InlineData(0.9, 1.0, 0.0, 0.0, 0.0, true)]
    [InlineData(1.0, 0.9, 0.0, 0.0, 0.0, true)]
    [InlineData(1.0, 1.0, 0.1, 0.0, 0.0, true)]
    [InlineData(1.0, 1.0, 0.0, 0.1, 0.0, true)]
    [InlineData(1.0, 1.0, 0.0, 0.0, 10.0, true)]
    [InlineData(1.0, 1.0, 0.0, 0.0, 0.0, false)]
    public void A_start_state_moves_when_it_scales_translates_or_turns(double scaleX, double scaleY, double translateX, double translateY, double rotate, bool moves)
    {
        var start = new RouteTransitionStart { ScaleX = scaleX, ScaleY = scaleY, TranslateX = translateX, TranslateY = translateY, Rotate = rotate };

        Assert.Equal(moves, start.Moves);
    }

    [Fact]
    public void A_start_state_is_a_value_two_equal_states_are_equal()
    {
        Assert.Equal(new RouteTransitionStart { Scale = 0.5 }, new RouteTransitionStart { Scale = 0.5 });
        Assert.NotEqual(new RouteTransitionStart { Scale = 0.5 }, new RouteTransitionStart { Scale = 0.6 });
    }

    // ------------------------------------------------------------------------------------------------ RegionChange.Resolve

    [Fact]
    public void An_unknown_change_has_no_direction_and_no_transition()
    {
        Assert.Equal(RouteableDirection.None, RegionChange.Unknown.Direction);
        Assert.Null(RegionChange.Unknown.Transition);
    }

    [Theory]
    [InlineData(RouteableDirection.None)]
    [InlineData(RouteableDirection.Forward)]
    [InlineData(RouteableDirection.Back)]
    public void The_direction_of_the_change_goes_to_the_resolved_transition(RouteableDirection direction)
        => Assert.Equal(direction, new RegionChange(direction, RouteTransition.Slide).Resolve(null, TimeSpan.Zero).Direction);

    [Fact]
    public void The_transition_of_the_navigation_wins_over_the_one_of_the_region()
    {
        var resolved = new RegionChange(RouteableDirection.Forward, RouteTransition.Slide).Resolve(RouteTransition.Fade, TimeSpan.Zero);

        Assert.Same(RouteTransition.Slide, resolved.Transition);
        Assert.True(resolved.Declared);
    }

    [Fact]
    public void The_transition_of_the_region_is_used_when_the_navigation_names_none()
    {
        var resolved = RegionChange.Unknown.Resolve(RouteTransition.SlideUp, TimeSpan.Zero);

        Assert.Same(RouteTransition.SlideUp, resolved.Transition);
        Assert.True(resolved.Declared);
    }

    [Fact]
    public void A_navigation_that_asks_for_none_stops_the_transition_of_the_region()
    {
        var resolved = new RegionChange(RouteableDirection.Forward, RouteTransition.None).Resolve(RouteTransition.Slide, TimeSpan.FromSeconds(1));

        Assert.True(resolved.IsNone);
        Assert.True(resolved.Declared);
    }

    [Fact]
    public void A_region_that_declares_none_is_declared_and_plays_nothing()
    {
        var resolved = RegionChange.Unknown.Resolve(RouteTransition.None, TimeSpan.FromSeconds(1));

        Assert.True(resolved.Declared);
        Assert.True(resolved.IsNone);
    }

    [Fact]
    public void The_legacy_duration_of_the_region_is_a_fade_that_is_not_declared()
    {
        var resolved = RegionChange.Unknown.Resolve(null, TimeSpan.FromMilliseconds(220));

        Assert.Same(RouteTransition.Fade, resolved.Transition);
        Assert.False(resolved.Declared);
        Assert.Equal(TimeSpan.FromMilliseconds(220), resolved.Duration);
        Assert.False(resolved.IsNone);
    }

    [Fact]
    public void Nothing_at_all_resolves_to_none_that_is_not_declared()
    {
        var resolved = RegionChange.Unknown.Resolve(null, TimeSpan.Zero);

        Assert.True(resolved.IsNone);
        Assert.False(resolved.Declared);
        Assert.Same(RouteTransition.None, resolved.Transition);
    }

    [Fact]
    public void A_negative_legacy_duration_is_the_same_as_none()
    {
        var resolved = RegionChange.Unknown.Resolve(null, TimeSpan.FromSeconds(-1));

        Assert.True(resolved.IsNone);
    }

    [Fact]
    public void A_transition_with_its_own_duration_ignores_the_duration_of_the_region()
    {
        var change = new RegionChange(RouteableDirection.Forward, RouteTransition.Slide.WithDuration(TimeSpan.FromMilliseconds(500)));

        Assert.Equal(TimeSpan.FromMilliseconds(500), change.Resolve(null, TimeSpan.FromSeconds(3)).Duration);
    }

    [Fact]
    public void A_transition_without_a_duration_takes_the_duration_of_the_region_then_the_default()
    {
        var change = new RegionChange(RouteableDirection.Forward, RouteTransition.Slide);

        Assert.Equal(TimeSpan.FromSeconds(3), change.Resolve(null, TimeSpan.FromSeconds(3)).Duration);
        Assert.Equal(RouteTransition.DefaultDuration, change.Resolve(null, TimeSpan.Zero).Duration);
        Assert.Equal(RouteTransition.DefaultDuration, change.Resolve(null, TimeSpan.FromSeconds(-2)).Duration);
    }

    [Fact]
    public void A_transition_whose_own_duration_is_zero_plays_nothing()
    {
        var change = new RegionChange(RouteableDirection.Forward, RouteTransition.Slide.WithDuration(TimeSpan.Zero));

        Assert.True(change.Resolve(null, TimeSpan.FromSeconds(1)).IsNone);
    }

    [Fact]
    public void A_region_transition_of_the_navigation_is_a_custom_name_when_it_is_not_built_in()
    {
        var resolved = new RegionChange(RouteableDirection.Back, RouteTransition.Named("Grow")).Resolve(null, TimeSpan.Zero);

        Assert.Equal("Grow", resolved.Transition.Name);
        Assert.False(resolved.IsNone);
        Assert.Equal(RouteableDirection.Back, resolved.Direction);
    }

    [Fact]
    public void A_region_transition_is_a_value_two_equal_results_are_equal()
    {
        var change = new RegionChange(RouteableDirection.Forward, RouteTransition.Fade);

        Assert.Equal(change.Resolve(null, TimeSpan.Zero), change.Resolve(null, TimeSpan.Zero));
    }

    [Fact]
    public void Resolving_every_built_in_name_with_every_direction_never_throws_and_always_has_a_positive_duration_unless_none()
    {
        foreach (var transition in RouteTransition.BuiltIn)
            foreach (var direction in Enum.GetValues<RouteableDirection>())
            {
                var resolved = new RegionChange(direction, transition).Resolve(null, TimeSpan.Zero);
                if (transition.IsNone)
                    Assert.True(resolved.IsNone);
                else
                    Assert.True(resolved.Duration > TimeSpan.Zero, $"{transition.Name} {direction}");
            }
    }
}
