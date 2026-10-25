#if WINDOWS

using Routeables.Abstraction;
using Routeables.Services;

/// <summary>
/// A toast-style notification card, visually similar to Avalonia's built-in
/// WindowNotificationManager cards: a colored left accent bar, a type icon, rounded
/// corners, drop shadow, and a slide-in-from-the-right + fade-out animation. Cards stack
/// top-right and shift to fill the gap when one closes.
/// </summary>
public static class RouteableToastHelper
{
    const string HostTag = "Routeables.ToastHost";
    const double CardWidth = 360;

    public static Microsoft.UI.Xaml.FrameworkElement? Show(
        string title,
        string message,
        RouteableNotificationType type,
        TimeSpan? expiration,
        Action? onClick,
        Action? onClose)
    {
        var host = GetOrCreateHost();
        if (host is null)
            return null!;

        var initialTheme = Application.Current?.RequestedTheme ?? AppTheme.Light;
        var resolvedInitialTheme = initialTheme == AppTheme.Unspecified
            ? Application.Current?.PlatformAppTheme ?? AppTheme.Light
            : initialTheme;

        var (accent, iconGlyph) = GetStyle(type, resolvedInitialTheme);

        var (card, closeButton, applyTheme) = BuildCard(title, message, accent, iconGlyph);

        applyTheme(initialTheme);

        void OnThemeChanged(object? s, AppThemeChangedEventArgs e) => applyTheme(e.RequestedTheme);
        if (Application.Current is not null)
            Application.Current.RequestedThemeChanged += OnThemeChanged;

        var session = new RouteableNotificationSession(title, message, type, expiration, onClick, onClose);
        var released = false;
        Microsoft.UI.Dispatching.DispatcherQueueTimer? timer = null;

        // What ties the card to the application: the theme subscription and the timer. Let go of both when the toast is
        // dismissed or when the card is unloaded (a window closed before the toast was dismissed).
        void Release()
        {
            if (released) return;
            released = true;

            if (timer is not null)
            {
                timer.Stop();
                timer = null;
            }

            if (Application.Current is not null)
                Application.Current.RequestedThemeChanged -= OnThemeChanged;
        }

        void Dismiss()
        {
            if (session.IsClosed) return;

            Release();

            AnimateOut(card, () =>
            {
                host.Children.Remove(card);
                session.NotifyClosed();
            });
        }

        // A tap is the answer to the notification: it runs onClick, if any, and dismisses it.
        card.PointerPressed += (_, e) =>
        {
            session.Click();
            Dismiss();
            e.Handled = true;
        };

        closeButton.Click += (_, _) => Dismiss();
        // The card is gone (dismissed, or its window closed first): the close callback runs once in either case.
        card.Unloaded += (_, _) =>
        {
            Release();
            session.NotifyClosed();
        };

        if (session.AutoCloseAfter is { } delay)
        {
            timer = card.DispatcherQueue.CreateTimer();
            timer.Interval = delay;
            timer.IsRepeating = false;
            timer.Tick += (_, _) => Dismiss();
            timer.Start();
        }

        host.Children.Insert(0, card); // newest on top
        AnimateIn(card);

        return card;
    }

    // Accent colors stay the same hue across themes (slightly brighter in dark mode, so
    // they read clearly against a dark card), matching Avalonia's severity palette.
    static (Microsoft.UI.Xaml.Media.Brush Accent, string Glyph) GetStyle(RouteableNotificationType type, AppTheme theme)
    {
        var dark = theme == AppTheme.Dark;
        return type switch
        {
            RouteableNotificationType.Success => (SolidColor(dark ? (0xFF, 0x4C, 0xC2, 0x6A) : (0xFF, 0x2E, 0xA0, 0x44)), "\uE73E"),
            RouteableNotificationType.Warning => (SolidColor(dark ? (0xFF, 0xF5, 0xB7, 0x5C) : (0xFF, 0xE0, 0x9F, 0x3E)), "\uE7BA"),
            RouteableNotificationType.Error => (SolidColor(dark ? (0xFF, 0xFF, 0x5C, 0x72) : (0xFF, 0xE8, 0x1E, 0x3E)), "\uEA39"),
            _ => (SolidColor(dark ? (0xFF, 0x6C, 0xA6, 0xF5) : (0xFF, 0x3E, 0x7B, 0xE0)), "\uE946"),
        };
    }

    static Microsoft.UI.Xaml.Media.SolidColorBrush SolidColor((int a, int r, int g, int b) c)
    => new(Windows.UI.Color.FromArgb((byte)c.a, (byte)c.r, (byte)c.g, (byte)c.b));

    /// <summary>Card chrome colors for one theme: background, border, title, and message text.</summary>
    readonly record struct CardPalette(
        Windows.UI.Color Background, Windows.UI.Color Border,
        Windows.UI.Color Title, Windows.UI.Color Message, Windows.UI.Color CloseGlyph);

    static CardPalette GetPalette(AppTheme theme) => theme == AppTheme.Dark
        ? new CardPalette(
            Background: Windows.UI.Color.FromArgb(0xFA, 0x2C, 0x2C, 0x2E),
            Border: Windows.UI.Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF),
            Title: Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF),
            Message: Windows.UI.Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF),
            CloseGlyph: Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF))
        : new CardPalette(
            Background: Windows.UI.Color.FromArgb(0xFA, 0xFF, 0xFF, 0xFF),
            Border: Windows.UI.Color.FromArgb(0x20, 0x00, 0x00, 0x00),
            Title: Windows.UI.Color.FromArgb(0xFF, 0x1B, 0x1B, 0x1B),
            Message: Windows.UI.Color.FromArgb(0xC0, 0x1B, 0x1B, 0x1B),
            CloseGlyph: Windows.UI.Color.FromArgb(0xFF, 0x1B, 0x1B, 0x1B));

    /// <summary>
    /// Builds the visual card: a left accent bar + icon + title/message + close button,
    /// wrapped in a rounded, shadowed border. Returns the top-level element to animate/host,
    /// the close button so the caller can wire its Click handler, and an applyTheme delegate
    /// that re-colors the card's chrome (background/border/text) for a given AppTheme —
    /// call it once up front and again whenever RequestedThemeChanged fires.
    /// </summary>
    static (Microsoft.UI.Xaml.Controls.Border Card, Microsoft.UI.Xaml.Controls.Button CloseButton, Action<AppTheme> ApplyTheme) BuildCard(
        string title, string message,
        Microsoft.UI.Xaml.Media.Brush accent, string glyph)
    {
        var icon = new Microsoft.UI.Xaml.Controls.FontIcon
        {
            Glyph = glyph,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
            Foreground = accent, // accent color itself doesn't change with theme (see GetStyle)
            FontSize = 18,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Top,
            Margin = new Microsoft.UI.Xaml.Thickness(0, 2, 12, 0),
        };

        var titleBlock = new Microsoft.UI.Xaml.Controls.TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 14,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
        };

        var messageBlock = new Microsoft.UI.Xaml.Controls.TextBlock
        {
            Text = message,
            FontSize = 13,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            Margin = new Microsoft.UI.Xaml.Thickness(0, 2, 0, 0),
        };

        var textStack = new Microsoft.UI.Xaml.Controls.StackPanel
        {
            Children = { titleBlock, messageBlock },
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
        };

        var closeIcon = new Microsoft.UI.Xaml.Controls.FontIcon
        {
            Glyph = "\uE711",
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
            FontSize = 11,
        };

        var closeButton = new Microsoft.UI.Xaml.Controls.Button
        {
            Content = closeIcon,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Microsoft.UI.Xaml.Thickness(0),
            Padding = new Microsoft.UI.Xaml.Thickness(4),
            Width = 28,
            Height = 28,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Top,
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Right,
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(4),
        };

        var grid = new Microsoft.UI.Xaml.Controls.Grid
        {
            ColumnDefinitions =
                {
                    new() { Width = Microsoft.UI.Xaml.GridLength.Auto },
                    new() { Width = new Microsoft.UI.Xaml.GridLength(1, Microsoft.UI.Xaml.GridUnitType.Star) },
                    new() { Width = Microsoft.UI.Xaml.GridLength.Auto },
                },
        };
        grid.Children.Add(icon);
        grid.Children.Add(textStack);
        grid.Children.Add(closeButton);
        Microsoft.UI.Xaml.Controls.Grid.SetColumn(icon, 0);
        Microsoft.UI.Xaml.Controls.Grid.SetColumn(textStack, 1);
        Microsoft.UI.Xaml.Controls.Grid.SetColumn(closeButton, 2);

        var backgroundBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush();
        var borderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush();
        var titleBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush();
        var messageBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush();
        var closeGlyphBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush();

        titleBlock.Foreground = titleBrush;
        messageBlock.Foreground = messageBrush;
        closeIcon.Foreground = closeGlyphBrush;

        // Rounded card with a flat left edge, so the accent bar sits flush against it.
        var card = new Microsoft.UI.Xaml.Controls.Border
        {
            Width = CardWidth,
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(0, 8, 8, 0),
            Background = backgroundBrush,
            BorderBrush = borderBrush,
            BorderThickness = new Microsoft.UI.Xaml.Thickness(1, 1, 1, 1),
            Padding = new Microsoft.UI.Xaml.Thickness(20, 12, 12, 12),
            Child = grid,
        };

        var accentBar = new Microsoft.UI.Xaml.Controls.Border
        {
            Width = 4,
            Background = accent,
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(2, 0, 0, 2),
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch,
        };

        var layered = new Microsoft.UI.Xaml.Controls.Grid();
        layered.Children.Add(card);
        layered.Children.Add(accentBar);

        // Outer shell: carries the shadow, margin between stacked cards, and right alignment.
        var shell = new Microsoft.UI.Xaml.Controls.Border
        {
            Child = layered,
            Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 10),
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Right,
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(8),
            Shadow = new Microsoft.UI.Xaml.Media.ThemeShadow(),
            Translation = new System.Numerics.Vector3(0, 0, 32), // lift above content for the shadow to render
            RenderTransform = new Microsoft.UI.Xaml.Media.TranslateTransform(),
        };

        void ApplyTheme(AppTheme theme)
        {
            // AppTheme.Unspecified means "follow the system", so resolve it to an
            // actual light/dark value using the current system setting.
            var resolved = theme == AppTheme.Unspecified
                ? Application.Current?.PlatformAppTheme ?? AppTheme.Light
                : theme;

            var palette = GetPalette(resolved);

            backgroundBrush.Color = palette.Background;
            borderBrush.Color = palette.Border;
            titleBrush.Color = palette.Title;
            messageBrush.Color = palette.Message;
            closeGlyphBrush.Color = palette.CloseGlyph;
        }

        return (shell, closeButton, ApplyTheme);
    }

    static void AnimateIn(Microsoft.UI.Xaml.FrameworkElement card)
    {
        card.Opacity = 0;
        var transform = card.RenderTransform as Microsoft.UI.Xaml.Media.TranslateTransform
            ?? new Microsoft.UI.Xaml.Media.TranslateTransform();
        transform.X = 40;
        card.RenderTransform = transform;

        var opacityAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut },
        };
        var slideAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            From = 40,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut },
        };

        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(opacityAnim, card);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(opacityAnim, "Opacity");
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(slideAnim, transform);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(slideAnim, "X");

        var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        sb.Children.Add(opacityAnim);
        sb.Children.Add(slideAnim);
        sb.Begin();
    }

    static void AnimateOut(Microsoft.UI.Xaml.FrameworkElement card, Action onComplete)
    {
        var opacityAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(160),
            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn },
        };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(opacityAnim, card);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(opacityAnim, "Opacity");

        var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        sb.Children.Add(opacityAnim);
        sb.Completed += (_, _) => onComplete();
        sb.Begin();
    }

    static Microsoft.UI.Xaml.Controls.StackPanel? GetOrCreateHost()
    {
        var existing = FindHost();
        if (existing is not null)
            return existing;

        if (GetRootPanel() is not { } root)
            return null;

        var host = new Microsoft.UI.Xaml.Controls.StackPanel
        {
            Tag = HostTag,
            Orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical,
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Right,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Top,
            Margin = new Microsoft.UI.Xaml.Thickness(0, 16, 16, 0),
        };

        if (root is Microsoft.UI.Xaml.Controls.Grid)
            Microsoft.UI.Xaml.Controls.Grid.SetRowSpan(host, 99);

        root.Children.Add(host);
        return host;
    }

    static Microsoft.UI.Xaml.Controls.StackPanel? FindHost()
    {
        if (GetRootPanel() is not { } root)
            return null;

        return root.Children
            .OfType<Microsoft.UI.Xaml.Controls.StackPanel>()
            .FirstOrDefault(p => Equals(p.Tag, HostTag));
    }

    static Microsoft.UI.Xaml.Controls.Panel? GetRootPanel()
    {
        var window = Application.Current?.Windows?.FirstOrDefault();
        var winui = window?.Handler.PlatformView as Microsoft.UI.Xaml.Window;
        return winui?.Content as Microsoft.UI.Xaml.Controls.Panel;
    }
}

#endif