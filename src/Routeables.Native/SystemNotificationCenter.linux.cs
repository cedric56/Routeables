#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS

using Routeables.Native.Desktop;

namespace Routeables.Native;

// Linux, from the plain net10.0 target: org.freedesktop.Notifications on the D-Bus session bus, which every desktop environment serves (GNOME, KDE, XFCE,
// dunst...), spoken by the small client of Desktop/DBusConnection.cs (no package). A tap on the notification (the "default" action) is a tap; the user
// closing it, or the server expiring it, is a dismissal.
public sealed partial class SystemNotificationCenter
{
    private const string LinuxService = "org.freedesktop.Notifications";
    private const string LinuxPath = "/org/freedesktop/Notifications";

    private readonly object _linuxGate = new();
    private readonly Dictionary<uint, uint> _linuxServerIds = [];     // our id -> the id the server gave
    private readonly Dictionary<uint, uint> _linuxIds = [];           // the id the server gave -> ours
    private DBusConnection? _linuxConnection;
    private bool _linuxTried;

    /// <summary>Whether there is a session bus (and so, usually, a desktop to show the notifications).</summary>
    private bool LinuxIsAvailable()
    {
        try { return LinuxConnection() is not null; }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException) { return false; }
    }

    private DBusConnection? LinuxConnection()
    {
        lock (_linuxGate)
        {
            if (_linuxTried)
                return _linuxConnection;

            _linuxTried = true;
            var task = Task.Run(DBusConnection.TryConnectAsync);
            if (!task.Wait(TimeSpan.FromSeconds(5)))
                return null;

            _linuxConnection = task.Result;
            if (_linuxConnection is null)
                return null;

            _linuxConnection.SignalReceived += OnLinuxSignal;
            _linuxConnection.Closed += () => { lock (_linuxGate) { _linuxConnection = null; _linuxTried = false; } };
            _ = _linuxConnection.CallAsync("org.freedesktop.DBus", "/org/freedesktop/DBus", "org.freedesktop.DBus", "AddMatch", "s",
                w => w.String($"type='signal',interface='{LinuxService}'"));
            return _linuxConnection;
        }
    }

    private void LinuxPresent(SystemNotification notification)
    {
        var connection = LinuxConnection() ?? throw new InvalidOperationException("There is no session bus to show the notification.");
        var appName = Options?.ChannelName ?? AppDomain.CurrentDomain.FriendlyName;

        // Notify(app_name, replaces_id, app_icon, summary, body, actions, hints, expire_timeout) -> id
        var reply = connection.CallAsync(LinuxService, LinuxPath, LinuxService, "Notify", "susssasa{sv}i", w =>
        {
            w.String(appName);
            w.UInt32(0);
            w.String(notification.Icon ?? string.Empty);
            w.String(notification.Title ?? string.Empty);
            w.String(notification.Message ?? string.Empty);

            // The action "default" is what a click on the notification itself sends.
            w.Align(4);
            var actionsLengthAt = w.Length;
            w.UInt32(0);
            var actionsStart = w.Length;
            w.String("default");
            w.String("Open");
            w.SetUInt32(actionsLengthAt, (uint)(w.Length - actionsStart));

            // No hint; the array of dictionary entries is aligned on 8 even when it is empty.
            w.UInt32(0);
            w.Align(8);

            w.Int32(-1);
        });

        if (!reply.Wait(TimeSpan.FromSeconds(3)))
            throw new TimeoutException("The notification server did not answer.");

        var serverId = reply.Result.Reader().UInt32();
        lock (_linuxGate)
        {
            _linuxServerIds[notification.Id] = serverId;
            _linuxIds[serverId] = notification.Id;
        }
    }

    private void LinuxClose(uint id)
    {
        uint serverId;
        lock (_linuxGate)
        {
            if (!_linuxServerIds.Remove(id, out serverId))
                return;
            _linuxIds.Remove(serverId);
        }

        _ = _linuxConnection?.CallAsync(LinuxService, LinuxPath, LinuxService, "CloseNotification", "u", w => w.UInt32(serverId));
    }

    private void OnLinuxSignal(DBusMessage message)
    {
        if (message.Interface != LinuxService)
            return;

        var reader = message.Reader();
        var serverId = reader.UInt32();
        uint id;
        lock (_linuxGate)
        {
            if (!_linuxIds.TryGetValue(serverId, out id))
                return;
        }

        if (message.Member == "ActionInvoked")
        {
            var action = reader.String();
            Complete(id, true, action == "default" ? null : action);
        }
        else if (message.Member == "NotificationClosed")
        {
            lock (_linuxGate)
            {
                _linuxIds.Remove(serverId);
                _linuxServerIds.Remove(id);
            }

            Complete(id, false, null);
        }
    }
}
#endif
