using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using NSubstitute;
using Routeables.Services;
using Routeables.Wpf.Services;
using Application = System.Windows.Application;
using Window = System.Windows.Window;
using static Routeables.Tests.Wpf.WpfTestWindows;

namespace Routeables.Tests.Wpf;

/// <summary>
/// The notifications of the WPF host: small windows owned by the active window, at the top centre of it, one under the other, placed in
/// pixels of the screen. The real monitors of the machine that runs the tests decide the scale; the placement is checked against the
/// scale of the owner, whatever it is. (Monitors of different scales are covered by <see cref="WpfNotificationLayoutTests"/>.)
/// </summary>
[Collection(WpfCollection.Name)]
public class WpfNotificationTests
{
    private static readonly TimeSpan Long = TimeSpan.FromMinutes(5);

    private static RouteableNotificationService Service() => new(new DefaultMessageBoxService());

    private static async Task WithOwnerAsync(Func<Window, Task> test)
    {
        var owner = await ShowOwnerAsync();
        try
        {
            await test(owner);
        }
        finally
        {
            await CleanUpAsync(owner);
        }
    }

    // A notification is a window without a frame: found by the title it shows.
    private static Window? FindNotification(string title)
        => Application.Current.Windows.OfType<Window>()
            .FirstOrDefault(w => w.IsVisible && w.WindowStyle == WindowStyle.None && Logical<TextBlock>(w).Any(t => t.Text == title));

    private static async Task<Window> WaitForNotificationAsync(string title, CancellationToken ct)
    {
        await WpfHarness.WaitUntil(() => FindNotification(title) is not null, $"the notification '{title}' is shown", ct);
        return (await WpfHarness.Run(() => FindNotification(title)))!;
    }

    private static int CountOfNotifications() => Application.Current.Windows.OfType<Window>().Count(w => w.IsVisible && w.WindowStyle == WindowStyle.None && w.Title != "owner");

    private static (int X, int Y, int Width, int Height) Bounds(Window window)
        => NativeMethods.TryGetBounds(new WindowInteropHelper(window).Handle, out var bounds)
            ? bounds
            : throw new InvalidOperationException($"The notification has no handle (IsVisible={window.IsVisible}, IsLoaded={window.IsLoaded}, Owner={(window.Owner is null ? "none" : "yes")}, Left={window.Left}, Top={window.Top}).");

    // What NotificationLayout says for the owner as it is now: its scale, its place on the screen, its width in pixels.
    private static (int X, int Y)[] Expected(Window owner, params Window[] notifications)
    {
        var scale = VisualTreeHelper.GetDpi(owner).DpiScaleX;
        var origin = owner.PointToScreen(new System.Windows.Point(0, 0));
        var width = (int)Math.Round(owner.ActualWidth * scale);
        var sizes = notifications.Select(n => (Bounds(n).Width, Bounds(n).Height)).ToArray();

        return [.. NotificationLayout.Stack((int)Math.Round(origin.X), (int)Math.Round(origin.Y), width, scale, sizes)];
    }

    // What a failing placement shows: the owner, and for each notification where it is and where it should be.
    private static string Describe(Window owner, params Window[] notifications)
    {
        var scale = VisualTreeHelper.GetDpi(owner).DpiScaleX;
        var origin = owner.PointToScreen(new System.Windows.Point(0, 0));
        var expected = Expected(owner, notifications);
        var lines = notifications.Select((n, i) =>
        {
            var b = Bounds(n);
            return $"  {(n.Content as FrameworkElement)?.GetType().Name} actual=({b.X},{b.Y}) size=({b.Width}x{b.Height}) expected=({expected[i].X},{expected[i].Y}) owner={(n.Owner is null ? "none" : "yes")}";
        });
        return $"owner origin=({origin.X:0.##},{origin.Y:0.##}) width={owner.ActualWidth:0.##} scale={scale}{Environment.NewLine}" + string.Join(Environment.NewLine, lines);
    }

    private static bool IsPlaced(Window owner, params Window[] notifications)
        => Expected(owner, notifications).SequenceEqual(notifications.Select(n => (Bounds(n).X, Bounds(n).Y)));

    private static Task ClickNotificationAsync(Window notification) => WpfHarness.Run(() =>
        notification.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonUpEvent,
            Source = notification
        }));

    [Fact]
    public Task A_notification_is_a_window_over_the_active_one_with_its_texts() => WithOwnerAsync(async owner =>
    {
        var ct = TestContext.Current.CancellationToken;

        Service().Show("Saved", "The file was saved.", RouteableNotificationType.Success, Long);
        var notification = await WaitForNotificationAsync("Saved", ct);

        await WpfHarness.Run(() =>
        {
            Assert.Same(owner, notification.Owner);
            Assert.False(notification.ShowInTaskbar);
            // The user keeps typing where they were.
            Assert.False(notification.ShowActivated);
            Assert.Contains("The file was saved.", Logical<TextBlock>(notification).Select(t => t.Text));
        });
    });

    [Fact]
    public Task A_click_closes_the_notification_and_tells_the_click_then_the_close_once() => WithOwnerAsync(async owner =>
    {
        var ct = TestContext.Current.CancellationToken;
        var log = new List<string>();

        Service().Show("Click me", "message", RouteableNotificationType.Information, Long, () => log.Add("click"), () => log.Add("close"));
        var notification = await WaitForNotificationAsync("Click me", ct);

        await ClickNotificationAsync(notification);
        await WpfHarness.WaitUntil(() => FindNotification("Click me") is null, "the notification is closed", ct);

        Assert.Equal(new[] { "click", "close" }, log);
    });

    [Fact]
    public Task A_notification_closes_when_its_time_is_up_without_a_click() => WithOwnerAsync(async owner =>
    {
        var ct = TestContext.Current.CancellationToken;
        var log = new List<string>();

        Service().Show("Short", "message", RouteableNotificationType.Warning, TimeSpan.FromMilliseconds(150), () => log.Add("click"), () => log.Add("close"));
        await WaitForNotificationAsync("Short", ct);

        await WpfHarness.WaitUntil(() => FindNotification("Short") is null, "the notification expires", ct);

        Assert.Equal(new[] { "close" }, log);
    });

    [Fact]
    public Task A_notification_without_an_expiration_stays_until_it_is_clicked() => WithOwnerAsync(async owner =>
    {
        var ct = TestContext.Current.CancellationToken;

        Service().Show("Stays", "message", RouteableNotificationType.Error, TimeSpan.Zero);
        var notification = await WaitForNotificationAsync("Stays", ct);
        await Task.Delay(400, ct);

        Assert.NotNull(await WpfHarness.Run(() => FindNotification("Stays")));

        await ClickNotificationAsync(notification);
        await WpfHarness.WaitUntil(() => FindNotification("Stays") is null, "the click closes it", ct);
    });

    [Fact]
    public Task Nothing_is_shown_when_the_active_window_cannot_display_it() => WithOwnerAsync(async owner =>
    {
        var box = Substitute.For<IMessageBoxService>();
        box.IsDisplayable(Arg.Any<Window>()).Returns(false);

        new RouteableNotificationService(box).Show("Hidden", "message", RouteableNotificationType.Information, Long);
        await WpfHarness.Idle();

        Assert.Equal(0, await WpfHarness.Run(CountOfNotifications));
        box.Received(1).IsDisplayable(owner);
    });

    [Fact]
    public Task Notifications_are_stacked_under_the_top_edge_of_the_owner_and_centred_in_pixels() => WithOwnerAsync(async owner =>
    {
        var ct = TestContext.Current.CancellationToken;
        var service = Service();
        var windows = new List<Window>();
        foreach (var title in new[] { "N1", "N2", "N3" })
        {
            service.Show(title, "a message that is long enough to make the window wider than a title alone", RouteableNotificationType.Information, Long);
            windows.Add(await WaitForNotificationAsync(title, ct));
        }

        await WpfHarness.WaitUntil(() => IsPlaced(owner, [.. windows]), "the notifications are laid out", ct, () => Describe(owner, [.. windows]));

        await WpfHarness.Run(() =>
        {
            var scale = VisualTreeHelper.GetDpi(owner).DpiScaleX;
            var origin = owner.PointToScreen(new System.Windows.Point(0, 0));
            var first = Bounds(windows[0]);

            // Independent of the layout code: the first is under the top edge by the margin, in pixels, and centred.
            Assert.Equal((int)Math.Round(origin.Y) + (int)Math.Round(16 * scale), first.Y);
            var ownerCentre = origin.X + owner.ActualWidth * scale / 2;
            Assert.InRange(first.X + first.Width / 2.0, ownerCentre - 2, ownerCentre + 2);

            // One under the other, in the order they were shown, without overlap.
            for (var i = 1; i < windows.Count; i++)
            {
                var above = Bounds(windows[i - 1]);
                var below = Bounds(windows[i]);
                Assert.True(below.Y >= above.Y + above.Height, $"N{i + 1} overlaps N{i}");
            }
        });
    });

    [Fact]
    public Task When_the_first_notification_closes_the_others_move_up() => WithOwnerAsync(async owner =>
    {
        var ct = TestContext.Current.CancellationToken;
        var service = Service();
        service.Show("First", "message", RouteableNotificationType.Information, Long);
        var first = await WaitForNotificationAsync("First", ct);
        service.Show("Second", "message", RouteableNotificationType.Information, Long);
        var second = await WaitForNotificationAsync("Second", ct);
        await WpfHarness.WaitUntil(() => IsPlaced(owner, first, second), "both are laid out", ct, () => Describe(owner, first, second));
        var firstTop = await WpfHarness.Run(() => Bounds(first).Y);
        Assert.NotEqual(firstTop, await WpfHarness.Run(() => Bounds(second).Y));

        await ClickNotificationAsync(first);

        await WpfHarness.WaitUntil(() => IsPlaced(owner, second), "the second takes the place of the first", ct);
        Assert.Equal(firstTop, await WpfHarness.Run(() => Bounds(second).Y));
    });

    [Fact]
    public Task Moving_or_resizing_the_owner_moves_the_notifications_with_it() => WithOwnerAsync(async owner =>
    {
        var ct = TestContext.Current.CancellationToken;
        Service().Show("Follow", "message", RouteableNotificationType.Information, Long);
        var notification = await WaitForNotificationAsync("Follow", ct);
        await WpfHarness.WaitUntil(() => IsPlaced(owner, notification), "the notification is laid out", ct, () => Describe(owner, notification));
        var before = await WpfHarness.Run(() => Bounds(notification));

        await WpfHarness.Run(() => owner.Left += 80);
        Assert.True(await WpfHarness.Run(() => notification.IsVisible), "the notification closed when the owner moved");
        await WpfHarness.WaitUntil(() => IsPlaced(owner, notification) && Bounds(notification).X > before.X, "the notification follows the move", ct, () => Describe(owner, notification));

        var moved = await WpfHarness.Run(() => Bounds(notification));
        await WpfHarness.Run(() => owner.Width = 500);
        Assert.True(await WpfHarness.Run(() => notification.IsVisible), "the notification closed when the owner was resized");
        await WpfHarness.WaitUntil(() => IsPlaced(owner, notification) && Bounds(notification).X != moved.X, "the notification is centred again", ct, () => Describe(owner, notification));
    });
}
