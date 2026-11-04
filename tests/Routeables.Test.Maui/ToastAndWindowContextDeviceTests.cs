using DeviceRunners.UITesting;
using Routeables.Maui.Services;
using Routeables.Services;
using Xunit;

namespace Routeables.Test.Maui;

/// <summary>
/// The toast made of MAUI views (<c>RouteableToastOverlay</c>, which the plain net10.0 target and the Reshapes fall back on), the service that
/// finds the page dialogs are shown over, and the base of the notification services, on the platform.
/// </summary>
public class ToastAndWindowContextDeviceTests
{
    // ------------------------------------------------------------------------------------------------ helpers for walking the visual tree

    /// <summary>Builds a bare <see cref="ContentPage"/> with the given content (or none), so each test starts from a known tree.</summary>
    private static ContentPage PageWith(View? content = null) => new() { Content = content };

    /// <summary>The toast wraps the page content in a <see cref="Grid"/>; this asserts that shape and hands back the layer.</summary>
    private static Grid Layer(ContentPage page) => Assert.IsType<Grid>(page.Content);

    /// <summary>The toast cards live in a single <see cref="VerticalStackLayout"/> inside the layer (the empty area that lets taps through).</summary>
    private static VerticalStackLayout Stack(ContentPage page) => Layer(page).Children.OfType<VerticalStackLayout>().Single();

    /// <summary>
    /// Depth-first enumeration of a subtree, unwrapping <see cref="Border.Content"/> and <see cref="Layout"/> children,
    /// so tests can search the whole visual tree with plain LINQ instead of hand-walking it.
    /// </summary>
    private static IEnumerable<IView> All(IView? root)
    {
        switch (root)
        {
            case null:
                yield break;
            case Border border:
                yield return border;
                foreach (var inner in All(border.Content)) yield return inner;
                break;
            case Layout layout:
                yield return layout;
                foreach (var child in layout.Children)
                    foreach (var inner in All(child)) yield return inner;
                break;
            default:
                yield return root;
                break;
        }
    }

    /// <summary>All label texts under a root, in tree order: the easiest way to assert what a toast actually shows.</summary>
    private static List<string> Texts(IView root) => [.. All(root).OfType<Label>().Select(l => l.Text)];

    /// <summary>The most recently added toast card: toasts append to the stack, so "last" is "newest".</summary>
    private static Border Card(ContentPage page) => Stack(page).Children.OfType<Border>().Last();

    /// <summary>
    /// <c>ITapGestureController</c> is internal to MAUI, so the recognizer's <c>Tapped</c> event is raised through its backing field instead.
    /// This simulates a user tap on a toast card without going through the platform gesture pipeline.
    /// </summary>
    private static void Tap(Border card)
    {
        var recognizer = card.GestureRecognizers.OfType<TapGestureRecognizer>().Single();
        var field = typeof(TapGestureRecognizer).GetField(nameof(TapGestureRecognizer.Tapped), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The Tapped event of TapGestureRecognizer has no backing field to raise it with.");
        ((EventHandler<TappedEventArgs>?)field.GetValue(recognizer))?.Invoke(recognizer, new TappedEventArgs(null));
    }

    // ------------------------------------------------------------------------------------------------ the toast

    /// <summary>
    /// The overlay must wrap the existing content (not replace it) in a Grid marked as the toast host,
    /// and the toast card must show both the title and the message.
    /// </summary>
    [UIFact]
    public void A_toast_wraps_the_content_of_the_page_once_and_shows_its_texts()
    {
        var original = new Label { Text = "the page" };
        var page = PageWith(original);

        RouteableToastOverlay.Show(page, "Saved", "All is well", RouteableNotificationType.Success, TimeSpan.FromMinutes(1), null, null);

        Assert.Contains(original, Layer(page).Children);
        Assert.Equal("Routeables.ToastHost", Layer(page).AutomationId);
        Assert.Contains("Saved", Texts(Card(page)));
        Assert.Contains("All is well", Texts(Card(page)));
    }

    /// <summary>
    /// Showing a second toast must reuse the existing host layer rather than wrapping the page again,
    /// and both cards must coexist in the same stack.
    /// </summary>
    [UIFact]
    public void A_second_toast_goes_to_the_same_layer_and_the_page_is_not_wrapped_again()
    {
        var page = PageWith(new Label());

        RouteableToastOverlay.Show(page, "One", "first", RouteableNotificationType.Information, TimeSpan.FromMinutes(1), null, null);
        var layer = page.Content;
        RouteableToastOverlay.Show(page, "Two", "second", RouteableNotificationType.Information, TimeSpan.FromMinutes(1), null, null);

        Assert.Same(layer, page.Content);
        Assert.Equal(2, Stack(page).Children.Count);
    }

    /// <summary>A page with no content must still be able to host a toast: the host layer is created around an empty page.</summary>
    [UIFact]
    public void A_page_without_content_still_gets_the_toast()
    {
        var page = PageWith();

        RouteableToastOverlay.Show(page, "t", "m", RouteableNotificationType.Warning, TimeSpan.FromMinutes(1), null, null);

        Assert.Single(Stack(page).Children);
    }

    /// <summary>
    /// The layer itself is input-transparent so the empty area does not swallow touches meant for the page,
    /// but it is not cascade-transparent so the toast cards inside still receive taps.
    /// </summary>
    [UIFact]
    public void The_empty_part_of_the_layer_lets_the_taps_through_to_the_page()
    {
        var page = PageWith(new Label());

        RouteableToastOverlay.Show(page, "t", "m", RouteableNotificationType.Error, TimeSpan.FromMinutes(1), null, null);

        Assert.True(Stack(page).InputTransparent);
        Assert.False(Stack(page).CascadeInputTransparent);
    }

    /// <summary>With an empty title the card should render just the message, without a stray title label.</summary>
    [UIFact]
    public void A_toast_without_a_title_shows_the_message_alone()
    {
        var page = PageWith(new Label());

        RouteableToastOverlay.Show(page, string.Empty, "only a message", RouteableNotificationType.Information, TimeSpan.FromMinutes(1), null, null);

        Assert.Equal(["only a message"], Texts(Card(page)).Where(t => t.Length > 1));
    }

    /// <summary>Each notification type maps to a distinct glyph, so the card visually conveys severity.</summary>
    [UIFact]
    public void Each_type_has_its_own_glyph()
    {
        var glyphs = new Dictionary<RouteableNotificationType, string>
        {
            [RouteableNotificationType.Success] = "✓",
            [RouteableNotificationType.Warning] = "!",
            [RouteableNotificationType.Error] = "✕",
            [RouteableNotificationType.Information] = "i",
        };

        foreach (var (type, glyph) in glyphs)
        {
            var page = PageWith(new Label());
            RouteableToastOverlay.Show(page, "t", "m", type, TimeSpan.FromMinutes(1), null, null);
            Assert.Contains(glyph, Texts(Card(page)));
        }
    }

    /// <summary>
    /// A tap fires <c>onClick</c> then <c>onClose</c> in that order, removes the card from the stack,
    /// and further taps must not fire the close callback a second time (idempotent dismissal).
    /// </summary>
    [UIFact]
    public void Tapping_a_toast_tells_the_click_then_the_close_and_removes_it_and_it_closes_only_once()
    {
        var page = PageWith(new Label());
        var events = new List<string>();
        RouteableToastOverlay.Show(page, "t", "m", RouteableNotificationType.Information, TimeSpan.FromMinutes(1),
            () => events.Add("click"), () => events.Add("close"));
        var card = Card(page);

        Tap(card);

        Assert.Equal(["click", "close"], events);
        Assert.Empty(Stack(page).Children);

        Tap(card);
        Assert.Single(events, "close");
    }

    /// <summary>When the expiration elapses, the toast auto-closes and invokes <c>onClose</c> exactly once.</summary>
    [UIFact]
    public async Task A_toast_closes_by_itself_after_its_expiration_and_tells_it()
    {
        var page = PageWith(new Label());
        var closed = 0;
        RouteableToastOverlay.Show(page, "t", "m", RouteableNotificationType.Information, TimeSpan.FromMilliseconds(80), null, () => closed++);

        await Device.UntilAsync(() => Stack(page).Children.Count == 0);

        Assert.Equal(1, closed);
    }

    /// <summary>
    /// A manual dismiss before expiration must suppress the timer's later close,
    /// so <c>onClose</c> is not fired twice for the same toast.
    /// </summary>
    [UIFact]
    public async Task A_toast_tapped_before_its_expiration_does_not_close_twice()
    {
        var page = PageWith(new Label());
        var closed = 0;
        RouteableToastOverlay.Show(page, "t", "m", RouteableNotificationType.Information, TimeSpan.FromMilliseconds(150), null, () => closed++);

        Tap(Card(page));
        await Task.Delay(300);

        Assert.Equal(1, closed);
    }

    /// <summary>
    /// The overlay resolves the "current page" of the common containers:
    /// <see cref="NavigationPage"/> → its current page, <see cref="TabbedPage"/> → the selected tab,
    /// <see cref="FlyoutPage"/> → the detail page.
    /// </summary>
    [UIFact]
    public void The_toast_goes_to_the_current_page_of_a_shell_a_navigation_page_tabs_and_a_flyout()
    {
        var inner = PageWith(new Label());
        var navigation = new NavigationPage(inner);
        RouteableToastOverlay.Show(navigation, "t", "m", RouteableNotificationType.Information, TimeSpan.FromMinutes(1), null, null);
        Assert.Single(Stack(inner).Children);

        var first = PageWith(new Label());
        var tabs = new TabbedPage { Children = { first, PageWith(new Label()) } };
        RouteableToastOverlay.Show(tabs, "t", "m", RouteableNotificationType.Information, TimeSpan.FromMinutes(1), null, null);
        Assert.Single(Stack(first).Children);

        var detail = PageWith(new Label());
        var flyout = new FlyoutPage { Flyout = new ContentPage { Title = "menu" }, Detail = detail };
        RouteableToastOverlay.Show(flyout, "t", "m", RouteableNotificationType.Information, TimeSpan.FromMinutes(1), null, null);
        Assert.Single(Stack(detail).Children);
    }

    /// <summary>
    /// If no host <see cref="ContentPage"/> can be found (an empty <see cref="NavigationPage"/>), the overlay must fail loudly
    /// rather than silently doing nothing.
    /// </summary>
    [UIFact]
    public void A_page_that_has_no_content_page_to_host_the_toast_is_refused()
    {
        var empty = new NavigationPage();

        Assert.Throws<InvalidOperationException>(() =>
            RouteableToastOverlay.Show(empty, "t", "m", RouteableNotificationType.Information, TimeSpan.FromMinutes(1), null, null));
    }

    // ------------------------------------------------------------------------------------------------ the window context

    /// <summary>
    /// The window context must report the page of the app's first window as the current page,
    /// which is what dialogs are shown over.
    /// </summary>
    [UIFact]
    public void The_window_context_gives_the_page_of_the_window_of_the_application()
    {
        var context = new RouteableDialogWindowContext();

        Assert.NotNull(context.CurrentPage);
        Assert.Same(Application.Current!.Windows[0].Page, context.CurrentPage);
    }

    /// <summary>
    /// The application's DI container must have registered an <see cref="IRouteableDialogWindowContext"/>,
    /// so consumers can resolve it in the running app.
    /// </summary>
    [UIFact]
    public void The_application_registered_the_window_context()
    {
        Assert.NotNull(Device.Services.GetService(typeof(IRouteableDialogWindowContext)));
    }

    // ------------------------------------------------------------------------------------------------ the base of a notification service

    /// <summary>
    /// Minimal concrete subclass that records <c>Show</c> calls; used to verify the base's default behavior
    /// for the members a platform-agnostic replacement does not override.
    /// </summary>
    private sealed class Quiet : RouteableNotificationServiceBase
    {
        public List<string> Shown { get; } = [];

        public override void Show(string title, string message, RouteableNotificationType type = RouteableNotificationType.Information,
            TimeSpan? expiration = null, Action? onClick = null, Action? onClose = null)
            => Shown.Add(title + ":" + message);
    }

    /// <summary>
    /// A subclass that does not use system notifications inherits safe defaults:
    /// no active notifications, no native notification object, no-op close/init, and usable events.
    /// </summary>
    [UIFact]
    public async Task A_replacement_without_system_notifications_has_the_defaults()
    {
        var service = new Quiet();

        service.Show("t", "m");
        Assert.Equal(["t:m"], service.Shown);
        Assert.Empty(service.ActiveNotifications());
        Assert.Null(await service.CreateNotificationAsync("category"));
        service.CloseAll();
        await service.InitializeAsync();

        // Subscribe/unsubscribe must not throw, proving the event surface is usable without system notifications.
        EventHandler<RouteableSystemNotificationEventArgs> handler = (_, _) => { };
        service.NotificationCompleted += handler;
        service.NotificationCompleted -= handler;
    }
}