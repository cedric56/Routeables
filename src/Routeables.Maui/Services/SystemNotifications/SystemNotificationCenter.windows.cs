#if WINDOWS

using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace Routeables.Maui.Services;

// Windows: AppNotificationManager of the Windows App SDK, for a packaged or an unpackaged application. Windows reports a tap, not a dismissal.
internal sealed partial class SystemNotificationCenter
{
    private const string IdKey = "id";

    private bool _registered;

    private partial Task<bool> RequestPermissionAsync()
    {
        try
        {
            var manager = AppNotificationManager.Default;
            if (!_registered)
            {
                _registered = true;
                manager.NotificationInvoked += (_, args) =>
                {
                    if (args.Arguments.TryGetValue(IdKey, out var text) && uint.TryParse(text, out var id))
                        Complete(id, activated: true, action: null);
                };
                manager.Register();
            }

            return Task.FromResult(manager.Setting == AppNotificationSetting.Enabled);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // The notification platform is not available to this application.
            return Task.FromResult(false);
        }
    }

    private partial void Present(SystemNotification notification)
    {
        var builder = new AppNotificationBuilder()
            .AddArgument(IdKey, notification.Id.ToString())
            .AddText(notification.Title ?? string.Empty)
            .AddText(notification.Message ?? string.Empty);

        var toast = builder.BuildNotification();
        toast.Tag = notification.Id.ToString();
        AppNotificationManager.Default.Show(toast);
    }

    private partial void Cancel(uint id) => _ = AppNotificationManager.Default.RemoveByTagAsync(id.ToString()).AsTask();

    private partial void CancelAll() => _ = AppNotificationManager.Default.RemoveAllAsync().AsTask();
}
#endif
