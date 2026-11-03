using DeviceRunners.UITesting;
using Routeables.Abstraction;
using Routeables.Maui;
using Routeables.Regions;
using Xunit;

namespace Routeables.Test.Maui;

/// <summary>
/// <see cref="RouteTransitions"/> on a native control: where each transition starts, that every one ends at rest, the custom
/// transitions, and a transition that cannot run.
/// </summary>
public class TransitionDeviceTests
{
    private static RegionTransition Declared(string name, NavigationDirection direction = NavigationDirection.Forward, int ms = 80)
        => new(RouteTransition.Named(name), TimeSpan.FromMilliseconds(ms), direction, Declared: true);

    private static Hosted<ContentView> Host() => new(new ContentView { Content = new Label { Text = "x" }, WidthRequest = 200, HeightRequest = 100 });

    public static TheoryData<string> BuiltInNames()
    {
        var data = new TheoryData<string>();
        foreach (var transition in RouteTransition.BuiltIn)
            data.Add(transition.Name);
        return data;
    }

    [UITheory]
    [MemberData(nameof(BuiltInNames))]
    public async Task Every_built_in_transition_ends_at_rest_in_both_directions(string name)
    {
        using var host = Host();

        await RouteTransitions.PlayAsync(host.View, Declared(name, NavigationDirection.Forward));
        Assert.True(Device.IsAtRest(host.View));

        await RouteTransitions.PlayAsync(host.View, Declared(name, NavigationDirection.Back));
        Assert.True(Device.IsAtRest(host.View));
    }

    [UIFact]
    public async Task A_transition_that_was_not_asked_for_leaves_the_control_as_it_is()
    {
        using var host = Host();
        host.View.Opacity = 0.3;
        host.View.TranslationX = 12;

        await RouteTransitions.PlayAsync(host.View, new RegionTransition(RouteTransition.None, TimeSpan.Zero, NavigationDirection.Forward, Declared: false));

        Assert.Equal(0.3, host.View.Opacity);
        Assert.Equal(12, host.View.TranslationX);
    }

    [UIFact]
    public async Task None_declared_puts_the_control_back_at_rest()
    {
        using var host = Host();
        host.View.Opacity = 0.3;
        host.View.TranslationX = 12;

        await RouteTransitions.PlayAsync(host.View, new RegionTransition(RouteTransition.None, TimeSpan.Zero, NavigationDirection.Forward, Declared: true));

        Assert.True(Device.IsAtRest(host.View));
    }

    [UIFact]
    public async Task Fade_starts_transparent()
    {
        using var host = Host();

        var playing = RouteTransitions.PlayAsync(host.View, Declared("Fade", ms: 400));

        Assert.True(host.View.Opacity < 1);
        await playing;
        Assert.Equal(1, host.View.Opacity);
    }

    [UIFact]
    public async Task Slide_starts_on_the_right_going_forward_and_on_the_left_going_back()
    {
        using var host = Host();

        var forward = RouteTransitions.PlayAsync(host.View, Declared("Slide", NavigationDirection.Forward, 400));
        var fromRight = host.View.TranslationX;
        await forward;
        var back = RouteTransitions.PlayAsync(host.View, Declared("Slide", NavigationDirection.Back, 400));
        var fromLeft = host.View.TranslationX;
        await back;

        Assert.True(fromRight > 0);
        Assert.True(fromLeft < 0);
        Assert.Equal(0, host.View.TranslationX);
    }

    [UIFact]
    public async Task SlideUp_starts_below_the_control()
    {
        using var host = Host();

        var playing = RouteTransitions.PlayAsync(host.View, Declared("SlideUp", ms: 400));

        Assert.True(host.View.TranslationY > 0);
        await playing;
        Assert.Equal(0, host.View.TranslationY);
    }

    [UIFact]
    public async Task A_new_play_replaces_the_one_that_is_still_running()
    {
        using var host = Host();
        var slow = RouteTransitions.PlayAsync(host.View, Declared("Slide", ms: 3000));

        await RouteTransitions.PlayAsync(host.View, Declared("Fade", ms: 60));
        await slow;

        Assert.True(Device.IsAtRest(host.View));
    }

    [UIFact]
    public async Task A_custom_transition_receives_the_control_the_direction_and_the_duration()
    {
        using var host = Host();
        View? seenView = null;
        NavigationDirection? seenDirection = null;
        TimeSpan? seenDuration = null;
        RouteTransitions.Register("DeviceCustomSeen", (view, direction, duration) =>
        {
            (seenView, seenDirection, seenDuration) = (view, direction, duration);
            return Task.CompletedTask;
        });

        await RouteTransitions.PlayAsync(host.View, Declared("DeviceCustomSeen", NavigationDirection.Back, 321));

        Assert.Same(host.View, seenView);
        Assert.Equal(NavigationDirection.Back, seenDirection);
        Assert.Equal(TimeSpan.FromMilliseconds(321), seenDuration);
    }

    [UIFact]
    public async Task A_custom_name_is_found_whatever_its_case()
    {
        using var host = Host();
        var calls = 0;
        RouteTransitions.Register("DeviceCustomCase", (_, _, _) => { calls++; return Task.CompletedTask; });

        await RouteTransitions.PlayAsync(host.View, Declared("devicecustomcase"));

        Assert.Equal(1, calls);
    }

    [UIFact]
    public async Task A_custom_transition_replaces_a_built_in_one_of_the_same_name()
    {
        using var host = Host();
        var calls = 0;
        RouteTransitions.Register("DeviceCustomOverride", (_, _, _) => { calls++; return Task.CompletedTask; });

        await RouteTransitions.PlayAsync(host.View, Declared("DeviceCustomOverride"));

        Assert.Equal(1, calls);
    }

    [UIFact]
    public async Task A_custom_transition_that_throws_leaves_the_control_at_rest()
    {
        using var host = Host();
        RouteTransitions.Register("DeviceCustomThrows", (view, _, _) =>
        {
            view.Opacity = 0;
            view.TranslationX = 50;
            throw new InvalidOperationException("boom");
        });

        await RouteTransitions.PlayAsync(host.View, Declared("DeviceCustomThrows"));

        Assert.True(Device.IsAtRest(host.View));
    }

    [UIFact]
    public async Task An_unknown_name_leaves_the_control_at_rest()
    {
        using var host = Host();
        host.View.Opacity = 0.2;

        await RouteTransitions.PlayAsync(host.View, Declared("NobodyKnowsThisOne"));

        Assert.True(Device.IsAtRest(host.View));
    }

    [Fact]
    public void Registering_a_transition_without_a_name_or_a_function_is_refused()
    {
        Assert.ThrowsAny<ArgumentException>(() => RouteTransitions.Register(" ", (_, _, _) => Task.CompletedTask));
        Assert.Throws<ArgumentNullException>(() => RouteTransitions.Register("DeviceCustomNull", null!));
    }

    [UIFact]
    public async Task A_control_that_never_had_a_handler_does_not_break_the_play()
    {
        var view = new ContentView();

        await RouteTransitions.PlayAsync(view, Declared("Rotate", ms: 60));

        Assert.True(Device.IsAtRest(view));
    }
}
