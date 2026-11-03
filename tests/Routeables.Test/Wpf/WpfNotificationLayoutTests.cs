using Routeables.Wpf.Services;

namespace Routeables.Tests.Wpf;

/// <summary>
/// Where the notifications go, in pixels of the screen. The monitors of a computer may have different scales: the owner window
/// tells the scale, and the numbers below are what a notification on a 100 %, 150 % or 200 % monitor gets.
/// </summary>
public class WpfNotificationLayoutTests
{
    private static readonly (int Width, int Height)[] One = [(300, 80)];

    [Fact]
    public void No_notification_gives_no_place() => Assert.Empty(NotificationLayout.Stack(0, 0, 1000, 1, []));

    [Theory]
    [InlineData(1.0, 16)]
    [InlineData(1.25, 20)]
    [InlineData(1.5, 24)]
    [InlineData(1.75, 28)]
    [InlineData(2.0, 32)]
    [InlineData(3.0, 48)]
    public void The_distance_from_the_top_is_the_same_size_to_the_eye_on_any_scale(double scale, int expectedPixels)
    {
        var place = Assert.Single(NotificationLayout.Stack(0, 100, 1000, scale, One));

        Assert.Equal(100 + expectedPixels, place.Y);
    }

    [Theory]
    [InlineData(1.0, 8)]
    [InlineData(1.5, 12)]
    [InlineData(2.0, 16)]
    public void The_gap_between_two_notifications_follows_the_scale(double scale, int expectedGap)
    {
        var places = NotificationLayout.Stack(0, 0, 1000, scale, [(300, 80), (300, 50), (300, 60)]);

        Assert.Equal(places[0].Y + 80 + expectedGap, places[1].Y);
        Assert.Equal(places[1].Y + 50 + expectedGap, places[2].Y);
    }

    [Fact]
    public void The_notifications_are_centred_on_the_owner_whatever_their_width()
    {
        var places = NotificationLayout.Stack(200, 0, 1000, 1, [(300, 40), (500, 40), (1000, 40)]);

        Assert.Equal(new[] { 550, 450, 200 }, places.Select(p => p.X).ToArray());
    }

    [Fact]
    public void An_odd_difference_of_width_is_cut_to_the_pixel_on_the_left()
    {
        var place = Assert.Single(NotificationLayout.Stack(0, 0, 1001, 1, [(300, 40)]));

        Assert.Equal(350, place.X);
    }

    [Fact]
    public void A_notification_wider_than_the_owner_overflows_on_both_sides()
    {
        var place = Assert.Single(NotificationLayout.Stack(500, 0, 200, 1, [(300, 40)]));

        Assert.Equal(450, place.X);
    }

    [Fact]
    public void A_monitor_on_the_left_of_the_main_one_has_negative_pixels_and_keeps_them()
    {
        // The window is on a second monitor placed to the left and above the main one.
        var place = Assert.Single(NotificationLayout.Stack(-1920, -300, 1920, 1, One));

        Assert.Equal((-1920 + (1920 - 300) / 2, -300 + 16), place);
    }

    [Fact]
    public void Two_owners_on_monitors_of_different_scales_each_get_the_margin_of_their_own()
    {
        var onMain = Assert.Single(NotificationLayout.Stack(0, 0, 1600, 1.0, One));
        var onSecond = Assert.Single(NotificationLayout.Stack(1920, 0, 2400, 2.0, One));

        Assert.Equal(16, onMain.Y);
        Assert.Equal(32, onSecond.Y);
        // Each one is centred in its own owner: the pixels of one monitor are not the units of the other.
        Assert.Equal((1600 - 300) / 2, onMain.X);
        Assert.Equal(1920 + (2400 - 300) / 2, onSecond.X);
    }

    [Fact]
    public void The_order_of_the_sizes_is_the_order_of_the_places_oldest_on_top()
    {
        var places = NotificationLayout.Stack(0, 0, 1000, 1, [(100, 10), (100, 20), (100, 30)]);

        Assert.True(places[0].Y < places[1].Y && places[1].Y < places[2].Y);
        Assert.Equal(3, places.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_scale_that_is_not_greater_than_zero_is_refused(double scale)
        => Assert.Throws<ArgumentOutOfRangeException>(() => NotificationLayout.Stack(0, 0, 1000, scale, One));

    [Fact]
    public void The_sizes_are_required() => Assert.Throws<ArgumentNullException>(() => NotificationLayout.Stack(0, 0, 1000, 1, null!));
}
