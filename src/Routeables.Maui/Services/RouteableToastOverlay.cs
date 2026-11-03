using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Maui.Services;

/// <summary>
/// A toast made of MAUI views, for the platforms without a native toast helper (iOS and Mac Catalyst):
/// the card is added to a layer over the content of the current page, so the page stays usable under it.
/// </summary>
internal static class RouteableToastOverlay
{
    private const string HostId = "Routeables.ToastHost";

    public static void Show(
        Page page,
        string title,
        string message,
        RouteableNotificationType type,
        TimeSpan? expiration,
        Action? onClick,
        Action? onClose)
    {
        var host = GetOrCreateHost(page)
            ?? throw new InvalidOperationException("No page content to attach the notification.");

        var session = new RouteableNotificationSession(title, message, type, expiration, onClick, onClose);
        var card = BuildCard(session.Title, session.Message, session.Type);

        void Dismiss()
        {
            if (session.IsClosed)
                return;

            host.Remove(card);
            session.NotifyClosed();
        }

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            session.Click();
            Dismiss();
        };
        card.GestureRecognizers.Add(tap);

        card.Opacity = 0;
        host.Add(card);
        _ = card.FadeToAsync(1, 150);

        if (session.AutoCloseAfter is { } delay)
        {
            // Read here, on the caller's thread: the timer continuation runs on a pool thread.
            var dispatcher = page.Dispatcher;
            _ = Task.Delay(delay).ContinueWith(
                _ => dispatcher.Dispatch(Dismiss),
                TaskScheduler.Default);
        }
    }

    // The page's own content is wrapped once in a Grid that also holds a stack of toasts.
    private static VerticalStackLayout? GetOrCreateHost(Page page)
    {
        if (FindContentPage(page) is not { } contentPage)
            return null;

        if (contentPage.Content is Grid { AutomationId: HostId } root)
            return root.Children.OfType<VerticalStackLayout>().LastOrDefault();

        var original = contentPage.Content;
        var stack = new VerticalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(16),
            VerticalOptions = LayoutOptions.End,
            HorizontalOptions = LayoutOptions.Fill,
            // The empty part of the layer must let taps through to the page.
            InputTransparent = true,
            CascadeInputTransparent = false
        };

        var wrapper = new Grid { AutomationId = HostId };
        contentPage.Content = null;
        if (original is not null)
            wrapper.Add(original);
        wrapper.Add(stack);
        contentPage.Content = wrapper;

        return stack;
    }

    private static ContentPage? FindContentPage(Page? page)
    {
        while (true)
        {
            switch (page)
            {
                case ContentPage content:
                    return content;
                case Shell shell:
                    page = shell.CurrentPage;
                    break;
                case NavigationPage navigation:
                    page = navigation.CurrentPage;
                    break;
                case TabbedPage tabs:
                    page = tabs.CurrentPage;
                    break;
                case FlyoutPage flyout:
                    page = flyout.Detail;
                    break;
                default:
                    return null;
            }
        }
    }

    private static Border BuildCard(string title, string message, RouteableNotificationType type)
    {
        var (glyph, light, dark) = type switch
        {
            RouteableNotificationType.Success => ("✓", "#107C10", "#6CCB5F"),
            RouteableNotificationType.Warning => ("!", "#9D5D00", "#FCE100"),
            RouteableNotificationType.Error => ("✕", "#C42B1C", "#FF99A4"),
            _ => ("i", "#0067C0", "#60CDFF")
        };

        var icon = new Label
        {
            Text = glyph,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Start,
            WidthRequest = 24,
            HorizontalTextAlignment = TextAlignment.Center
        };
        icon.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb(light), Color.FromArgb(dark));

        var texts = new VerticalStackLayout { Spacing = 2 };
        if (!string.IsNullOrWhiteSpace(title))
            texts.Add(RouteableNativeDialogLook.Text(title, 15, FontAttributes.Bold));
        texts.Add(RouteableNativeDialogLook.Text(message, 14, secondary: true));

        var row = new Grid
        {
            ColumnSpacing = 12,
            Padding = new Thickness(16, 12),
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }
        };
        row.Add(icon, 0, 0);
        row.Add(texts, 1, 0);

        var card = new Border
        {
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(12) },
            Content = row,
            Shadow = new Shadow { Opacity = 0.25f, Radius = 12, Offset = new Point(0, 4) }
        };
        card.SetAppThemeColor(Border.BackgroundColorProperty, Color.FromArgb("#F3F3F3"), Color.FromArgb("#2C2C2C"));
        card.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#D0D0D0"), Color.FromArgb("#4A4A4A"));
        return card;
    }
}
