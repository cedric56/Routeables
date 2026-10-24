using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Routeables.Services;
using Routeables.Avalonia;

namespace Routeables.Avalonia.Services;

internal class RouteableNotificationService(IMessageBoxService service) : IRouteableNotificationService
{
    private WindowNotificationManager? _manager;

    public RouteableNotificationPosition Position => RouteableNotificationPosition.TopCenter;

    public void Show(string title, string message, RouteableNotificationType type = default,
                     TimeSpan? expiration = null, Action? onClick = null, Action? onClose = null)
    {
        var topLevel = TopLevelStateManager.Default.GetActive();
        if (topLevel == null)
            return;

        // Always check and update manager
        if (_manager == null || (TopLevel.GetTopLevel(_manager) != topLevel && service.IsDisplayable(topLevel)))
        {
            // The notification waits for the template of the manager it was created for. The handler removes itself,
            // so a manager that is replaced before its template is applied does not keep this notification alive.
            var manager = _manager = new WindowNotificationManager(topLevel);

            void Ready(object? sender, TemplateAppliedEventArgs e)
            {
                manager.TemplateApplied -= Ready;
                Display(manager);
            }

            manager.TemplateApplied += Ready;
        }
        else
        {
            Display(_manager);
        }

        void Display(WindowNotificationManager target)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                // Avalonia keeps a notification with a zero expiration until it is closed.
                var session = new RouteableNotificationSession(title, message, type, expiration, onClick, onClose);
                var notification = new Notification(session.Title, session.Message, (NotificationType)session.Type,
                                                   session.AutoCloseAfter ?? TimeSpan.Zero, session.Click, () => session.NotifyClosed());
                target.Show(notification);
            });
        }
    }
}
