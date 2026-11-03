using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Routeables.Services;
using Routeables.Avalonia;

namespace Routeables.Avalonia.Services;

internal partial class RouteableNotificationService(IMessageBoxService service) : IRouteableNotificationService
{
    // The manager of the window the notifications are shown in. It draws nothing until its template is applied: what is shown
    // meanwhile waits in the slot, so two notifications asked in a row share one manager instead of creating a second one.
    private sealed class Slot(WindowNotificationManager manager, TopLevel host)
    {
        public WindowNotificationManager Manager { get; } = manager;
        public TopLevel Host { get; } = host;
        public bool Ready { get; set; }
        public List<Action> Waiting { get; } = [];
    }

    private Slot? _slot;

    public RouteableNotificationPosition Position => RouteableNotificationPosition.TopCenter;

    public void Show(string title, string message, RouteableNotificationType type = default,
                     TimeSpan? expiration = null, Action? onClick = null, Action? onClose = null)
    {
        // Controls belong to the UI thread: the manager is created and used there.
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Show(title, message, type, expiration, onClick, onClose));
            return;
        }

        var topLevel = TopLevelStateManager.Default.GetActive();
        if (topLevel == null)
            return;

        // A new manager only for another window (and only one the service can display in): the one of this window is kept, even
        // before it is attached to the visual tree, which is when asking the manager for its top level gives nothing yet.
        if (_slot is null || (!ReferenceEquals(_slot.Host, topLevel) && service.IsDisplayable(topLevel)))
        {
            var slot = _slot = new Slot(new WindowNotificationManager(topLevel), topLevel);

            void Ready(object? sender, TemplateAppliedEventArgs e)
            {
                slot.Manager.TemplateApplied -= Ready;
                slot.Ready = true;
                var waiting = slot.Waiting.ToArray();
                slot.Waiting.Clear();
                foreach (var show in waiting)
                    show();
            }

            slot.Manager.TemplateApplied += Ready;
        }

        var current = _slot;
        void Display()
        {
            // Avalonia keeps a notification with a zero expiration until it is closed.
            var session = new RouteableNotificationSession(title, message, type, expiration, onClick, onClose);
            var notification = new Notification(session.Title, session.Message, (NotificationType)session.Type,
                                               session.AutoCloseAfter ?? TimeSpan.Zero, session.Click, () => session.NotifyClosed());
            current.Manager.Show(notification);
        }

        if (current.Ready)
            Display();
        else
            current.Waiting.Add(Display);
    }
}
