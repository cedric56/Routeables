using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Routeables.Services;

namespace Routeables.Wpf.Services;

// WPF has no notification control: a notification is a small window owned by the active window, at the top centre of it,
// stacked under the ones already open. It closes when clicked or when its time is up. It is placed in pixels of the screen
// (NotificationLayout, NativeMethods): the units of Window.Left and Window.Top depend on the monitor, and the window may be
// shown on one and belong to another.
internal sealed class RouteableNotificationService(IMessageBoxService service) : IRouteableNotificationService
{
    // Notifications currently open, oldest first. Only touched on the UI thread.
    private static readonly List<Window> Open = [];

    public RouteableNotificationPosition Position => RouteableNotificationPosition.TopCenter;

    public void Show(string title, string message, RouteableNotificationType type = default,
                     TimeSpan? expiration = null, Action? onClick = null, Action? onClose = null)
    {
        UiThread.Post(() =>
        {
            var owner = RouteableWindows.Active;
            if (owner is null || !service.IsDisplayable(owner))
                return;

            Display(owner, new RouteableNotificationSession(title, message, type, expiration, onClick, onClose));
        });
    }

    private static void Display(Window owner, RouteableNotificationSession session)
    {
        var window = new Window
        {
            Owner = owner,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,          // the user keeps typing where they were
            SizeToContent = SizeToContent.WidthAndHeight,
            MaxWidth = 420,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Content = Create(session)
        };

        // The window is first shown on the monitor of its owner (its position is exact once its size is known: Arrange below).
        window.Left = owner.Left;
        window.Top = owner.Top;
        _ = new WindowInteropHelper(window).EnsureHandle();

        void Place(object? sender, EventArgs e) => Arrange(owner);
        window.SizeChanged += Place;
        // WPF places a window again when it shows it and when it renders it for the first time: the place is given after that too.
        window.Loaded += Place;
        window.ContentRendered += Place;
        owner.LocationChanged += Place;
        owner.SizeChanged += Place;

        window.MouseLeftButtonUp += (_, _) =>
        {
            session.Click();
            window.Close();
        };

        DispatcherTimer? timer = null;
        if (session.AutoCloseAfter is { } after)
        {
            timer = new DispatcherTimer { Interval = after };
            timer.Tick += (_, _) => window.Close();
        }

        window.Closed += (_, _) =>
        {
            timer?.Stop();
            window.SizeChanged -= Place;
            window.Loaded -= Place;
            window.ContentRendered -= Place;
            owner.LocationChanged -= Place;
            owner.SizeChanged -= Place;
            Open.Remove(window);
            session.NotifyClosed();
            Arrange(owner);
        };

        Open.Add(window);
        window.Show();
        Arrange(owner);
        timer?.Start();
    }

    private static UIElement Create(RouteableNotificationSession session)
    {
        var accent = session.Type switch
        {
            RouteableNotificationType.Success => Color.FromRgb(0x2E, 0x9E, 0x5B),
            RouteableNotificationType.Warning => Color.FromRgb(0xE0, 0xA1, 0x00),
            RouteableNotificationType.Error => Color.FromRgb(0xD6, 0x45, 0x45),
            _ => Color.FromRgb(0x2D, 0x7F, 0xF9)
        };

        var texts = new StackPanel { Margin = new Thickness(12, 10, 14, 10) };
        if (!string.IsNullOrWhiteSpace(session.Title))
            texts.Children.Add(new TextBlock { Text = session.Title, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(session.Message))
            texts.Children.Add(new TextBlock { Text = session.Message, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 });

        var strip = new Border { Width = 5, Background = new SolidColorBrush(accent) };
        var row = new DockPanel { Cursor = Cursors.Hand };
        DockPanel.SetDock(strip, Dock.Left);
        row.Children.Add(strip);
        row.Children.Add(texts);

        var border = new Border { BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromArgb(64, 128, 128, 128)), Child = row };
        border.SetResourceReference(Border.BackgroundProperty, SystemColors.WindowBrushKey);
        border.SetResourceReference(TextElement.ForegroundProperty, SystemColors.WindowTextBrushKey);
        return border;
    }

    // Lays the open notifications out under the top edge of the window, centred, one under the other, in pixels of the screen.
    private static void Arrange(Window owner)
    {
        // A window that is closing has no presentation source any more: its notifications are closing too. A minimized one has no place.
        if (owner.WindowState == WindowState.Minimized || PresentationSource.FromVisual(owner) is null)
            return;

        var scale = VisualTreeHelper.GetDpi(owner).DpiScaleX;
        var origin = owner.PointToScreen(new Point(0, 0));          // pixels
        var ownerWidth = (int)Math.Round(owner.ActualWidth * scale);

        var placed = new List<IntPtr>();
        var sizes = new List<(int Width, int Height)>();
        foreach (var window in Open.Where(w => ReferenceEquals(w.Owner, owner)))
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (NativeMethods.TryGetSize(handle, out var size))
            {
                placed.Add(handle);
                sizes.Add(size);
            }
        }

        var places = NotificationLayout.Stack((int)Math.Round(origin.X), (int)Math.Round(origin.Y), ownerWidth, scale, sizes);
        for (var i = 0; i < placed.Count; i++)
            NativeMethods.TryMove(placed[i], places[i].X, places[i].Y);
    }
}
