using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Routeables.Abstraction;
using Routeables.Services;

namespace Routeables.Uno.Services;

/// <summary>
/// Toast notifications, shown at the top of the active window as <see cref="InfoBar"/>s in a popup that does not take the input
/// of what is behind it. A notification closes after <c>expiration</c> (five seconds when none is given, never with <c>RouteableNotificationDefaults.Never</c>), when the user closes it, or when the user taps it (after its onClick).
/// </summary>
internal class RouteableNotificationService(IMessageBoxService service) : IRouteableNotificationService
{
    // One popup per window: the notifications of a window stack up in it.
    private readonly ConditionalWeakTable<XamlRoot, NotificationHost> _hosts = new();

    public RouteableNotificationPosition Position => RouteableNotificationPosition.TopCenter;

    public void Show(string title, string message, RouteableNotificationType type = default,
                     TimeSpan? expiration = null, Action? onClick = null, Action? onClose = null)
    {
        var root = RouteableWindows.ActiveXamlRoot;
        if (root is null || !service.IsDisplayable(root))
            return;

        _ = UiThread.RunAsync(() =>
        {
            _hosts.GetValue(root, r => new NotificationHost(r)).Add(new RouteableNotificationSession(title, message, type, expiration, onClick, onClose));
            return Task.FromResult(0);
        });
    }

    private sealed class NotificationHost
    {
        private readonly XamlRoot _root;
        private readonly Popup _popup;
        private readonly Grid _surface;
        private readonly StackPanel _stack;

        public NotificationHost(XamlRoot root)
        {
            _root = root;
            _stack = new StackPanel
            {
                Spacing = 8,
                Margin = new Thickness(16),
                MaxWidth = 480,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
            };

            // A grid without a background takes no input: only the notifications do.
            _surface = new Grid();
            _surface.Children.Add(_stack);

            _popup = new Popup { XamlRoot = root, Child = _surface, IsLightDismissEnabled = false };
            Resize();
            root.Changed += (_, _) => Resize();
        }

        public void Add(RouteableNotificationSession session)
        {
            var bar = new InfoBar
            {
                Title = session.Title,
                Message = session.Message,
                Severity = session.Type switch
                {
                    RouteableNotificationType.Success => InfoBarSeverity.Success,
                    RouteableNotificationType.Warning => InfoBarSeverity.Warning,
                    RouteableNotificationType.Error => InfoBarSeverity.Error,
                    _ => InfoBarSeverity.Informational,
                },
                IsClosable = true,
                IsOpen = true,
            };

            // A tap is the answer to the notification: it runs onClick, if any, and dismisses it.
            // The close button of the bar is inside it: a tap on it only closes the notification, it is not the answer.
            bar.Tapped += (_, e) =>
            {
                if (IsInsideAButton(e.OriginalSource as DependencyObject, bar))
                    return;

                session.Click();
                bar.IsOpen = false;
            };

            DispatcherQueueTimer? timer = null;
            bar.Closed += (_, _) =>
            {
                timer?.Stop();
                _stack.Children.Remove(bar);
                if (_stack.Children.Count == 0)
                    _popup.IsOpen = false;

                session.NotifyClosed();
            };

            _stack.Children.Add(bar);
            Resize();
            _popup.IsOpen = true;

            if (session.AutoCloseAfter is { } delay && bar.DispatcherQueue is { } queue)
            {
                timer = queue.CreateTimer();
                timer.Interval = delay;
                timer.IsRepeating = false;
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    bar.IsOpen = false;
                };
                timer.Start();
            }
        }

        private static bool IsInsideAButton(DependencyObject? node, DependencyObject bar)
        {
            for (; node is not null && !ReferenceEquals(node, bar); node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node))
                if (node is ButtonBase)
                    return true;
            return false;
        }

        private void Resize()
        {
            _surface.Width = _root.Size.Width;
            _surface.Height = _root.Size.Height;
        }
    }
}