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
    private static ContentPage PageWith(View? content = null) => new() { Content = content };

    private static Grid Layer(ContentPage page) => Assert.IsType<Grid>(page.Content);

    private static VerticalStackLayout Stack(ContentPage page) => Layer(page).Children.OfType<VerticalStackLayout>().Single();

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

    private static List<string> Texts(IView root) => [.. All(root).OfType<Label>().Select(l => l.Text)];

    private static Border Card(ContentPage page) => Stack(page).Children.OfType<Border>().Last();

    private static void Tap(Border card) => ((ITapGestureController)card.GestureRecognizers.OfType<TapGestureRecognizer>().Single()).SendTapped(card);

    // ------------------------------------------------------------------------------------------------ the toast

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

    [UIFact]
    public void A_page_without_content_still_gets_the_toast()
    {
        var page = PageWith();

        RouteableToastOverlay.Show(page, "t", "m", RouteableNotificationType.Warning, TimeSpan.FromMinutes(1), null, null);

        Assert.Single(Stack(page).Children);
    }

    [UIFact]
    public void The_empty_part_of_the_layer_lets_the_taps_through_to_the_page()
    {
        var page = PageWith(new Label());

        RouteableToastOverlay.Show(page, "t", "m", RouteableNotificationType.Error, TimeSpan.FromMinutes(1), null, null);

        Assert.True(Stack(page).InputTransparent);
        Assert.False(Stack(page).CascadeInputTransparent);
    }

    [UIFact]
    public void A_toast_without_a_title_shows_the_message_alone()
    {
        var page = PageWith(new Label());

        RouteableToastOverlay.Show(page, string.Empty, "only a message", RouteableNotificationType.Information, TimeSpan.FromMinutes(1), null, null);

        Assert.Equal(["only a message"], Texts(Card(page)).Where(t => t.Length > 1));
    }

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

    [UIFact]
    public async Task A_toast_closes_by_itself_after_its_expiration_and_tells_it()
    {
        var page = PageWith(new Label());
        var closed = 0;
        RouteableToastOverlay.Show(page, "t", "m", RouteableNotificationType.Information, TimeSpan.FromMilliseconds(80), null, () => closed++);

        await Device.UntilAsync(() => Stack(page).Children.Count == 0);

        Assert.Equal(1, closed);
    }

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

    [UIFact]
    public void A_page_that_has_no_content_page_to_host_the_toast_is_refused()
    {
        var empty = new NavigationPage();

        Assert.Throws<InvalidOperationException>(() =>
            RouteableToastOverlay.Show(empty, "t", "m", RouteableNotificationType.Information, TimeSpan.FromMinutes(1), null, null));
    }

    // ------------------------------------------------------------------------------------------------ the window context

    [UIFact]
    public void The_window_context_gives_the_page_of_the_window_of_the_application()
    {
        var context = new RouteableDialogWindowContext();

        Assert.NotNull(context.CurrentPage);
        Assert.Same(Application.Current!.Windows[0].Page, context.CurrentPage);
    }

    [UIFact]
    public void The_application_registered_the_window_context()
    {
        Assert.NotNull(Device.Services.GetService(typeof(IRouteableDialogWindowContext)));
    }

    // ------------------------------------------------------------------------------------------------ the base of a notification service

    private sealed class Quiet : RouteableNotificationServiceBase
    {
        public List<string> Shown { get; } = [];

        public override void Show(string title, string message, RouteableNotificationType type = RouteableNotificationType.Information,
            TimeSpan? expiration = null, Action? onClick = null, Action? onClose = null)
            => Shown.Add(title + ":" + message);
    }

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

        EventHandler<RouteableSystemNotificationEventArgs> handler = (_, _) => { };
        service.NotificationCompleted += handler;
        service.NotificationCompleted -= handler;
    }
}
