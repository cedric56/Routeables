#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS

using Routeables.Native.Desktop;

namespace Routeables.Native;

// Linux, from the plain net10.0 target: org.freedesktop.Notifications on the D-Bus session bus, which every desktop environment serves (GNOME,
// KDE, XFCE, dunst...), spoken by the small client in Desktop/DBusConnection.cs (no package). A tap on the notification (the "default" action)
// is a tap; the user closing it, or the server expiring it, is a dismissal.
public sealed partial class SystemNotificationCenter
{
    private const string LinuxService = "org.freedesktop.Notifications";
    private const string LinuxPath = "/org/freedesktop/Notifications";

    private readonly object _linuxGate = new();
    private readonly Dictionary<uint, uint> _linuxServerIds = []; // our id to the id the server gave
    private readonly Dictionary<uint, uint> _linuxIds = []; // the id the server gave to ours
    private DBusConnection? _linuxConnection;
    private Task<DBusConnection?>? _linuxConnecting; // null: not tried yet, or the bus was closed

    private static readonly TimeSpan LinuxCallTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan LinuxRetryAfter = TimeSpan.FromSeconds(5);
    private DateTime _linuxFailedAt; // when the last connection failed (a bus that is not there yet is tried again later)
    private DateTime _linuxAvailableAt; // when the server last answered who it is

    /// <summary>Whether there is a session bus <i>and</i> a notification server on it: a session bus alone is not enough (WSL, a container or a bare window manager have one and
    /// no server, and the call to Notify fails with ServiceUnknown). The answer "yes" is kept for a few seconds.</summary>
    private async Task<bool> LinuxIsAvailableAsync()
    {
        lock (_linuxGate)
            if (DateTime.UtcNow - _linuxAvailableAt < TimeSpan.FromSeconds(10))
                return true;

        try
        {
            var connection = await LinuxConnectionAsync();
            if (connection is null)
                return false;

            // Asking the server who it is starts it when the bus can (D-Bus activation), and fails with ServiceUnknown when nothing serves the name.
            await connection.CallAsync(LinuxService, LinuxPath, LinuxService, "GetServerInformation", null, null, LinuxCallTimeout);
            lock (_linuxGate)
                _linuxAvailableAt = DateTime.UtcNow;
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or InvalidDataException) { return false; }
    }

    // The connection is made once, on the thread pool, and callers wait for it outside the lock. The lock only guards the fields, so a signal or a
    // close is never held up by a slow connection (a bus that does not answer). A failed connection is retried, but not at each call: only after
    // LinuxRetryAfter (the session bus is not always there yet when the application starts).
    private async Task<DBusConnection?> LinuxConnectionAsync()
    {
        Task<DBusConnection?> connecting;
        lock (_linuxGate)
        {
            if (_linuxConnecting is null && DateTime.UtcNow - _linuxFailedAt < LinuxRetryAfter)
                return null;
            connecting = _linuxConnecting ??= Task.Run(ConnectLinuxAsync);
        }

        DBusConnection? connection;
        try { connection = await connecting.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (TimeoutException) { return null; } // too slow for this call; the connection goes on in the background and the next call may find it

        if (connection is null)
        {
            lock (_linuxGate)
            {
                if (ReferenceEquals(_linuxConnecting, connecting))
                {
                    _linuxConnecting = null;
                    _linuxFailedAt = DateTime.UtcNow;
                }
            }
        }

        return connection;
    }

    private async Task<DBusConnection?> ConnectLinuxAsync()
    {
        DBusConnection? connection;
        try { connection = await DBusConnection.TryConnectAsync().ConfigureAwait(false); }
        catch (Exception) { return null; } // whatever stops the connection means there is no bus: Present reports it

        if (connection is null)
            return null;

        connection.SignalReceived += message => OnLinuxSignal(message);
        // A closed bus is connected again by the next call.
        connection.Closed += () =>
        {
            lock (_linuxGate) { _linuxConnection = null; _linuxConnecting = null; _linuxOwner = null; }
            EndServerNotifications();
        };
        lock (_linuxGate)
            _linuxConnection = connection;

        // Only what the notification server sends (its signals have its name as sender) and changes of who has that name: the bus writes a message's
        // sender, so a process cannot sign as the server. Otherwise any other process of the session could send ActionInvoked with an easily guessed
        // identifier and have an action run as if the user had clicked.
        // None of them is awaited, and none waits forever: a bus that does not answer would keep an entry for each call until the connection closes.
        Forget(connection.CallAsync(BusService, BusPath, BusService, "AddMatch", "s",
            w => w.String($"type='signal',sender='{LinuxService}',interface='{LinuxService}'"), LinuxCallTimeout));
        Forget(connection.CallAsync(BusService, BusPath, BusService, "AddMatch", "s",
            w => w.String($"type='signal',sender='{BusService}',interface='{BusService}',member='NameOwnerChanged',arg0='{LinuxService}'"), LinuxCallTimeout));
        _ = ReadOwnerAsync(connection);
        return connection;
    }

    private async Task LinuxPresentAsync(SystemNotification notification)
    {
        var connection = await LinuxConnectionAsync() ?? throw new InvalidOperationException("There is no session bus to show the notification.");
        // A notification that is shown again replaces the bubble the server already has (its own id), instead of adding a second one.
        uint replaces;
        lock (_linuxGate)
            _linuxServerIds.TryGetValue(notification.Id, out replaces);
        var appName = Options?.ChannelName ?? AppDomain.CurrentDomain.FriendlyName;

        // Notify(app_name, replaces_id, app_icon, summary, body, actions, hints, expire_timeout) returns an id.
        var reply = connection.CallAsync(LinuxService, LinuxPath, LinuxService, "Notify", "susssasa{sv}i", w =>
        {
            w.String(appName);
            w.UInt32(replaces);
            w.String(notification.Icon ?? string.Empty);
            w.String(notification.Title ?? string.Empty);
            w.String(notification.Message ?? string.Empty);

            // The action "default" is what a click on the notification itself sends.
            w.Align(4);
            var actionsLengthAt = w.Length;
            w.UInt32(0);
            var actionsStart = w.Length;
            w.String("default");
            w.String(string.IsNullOrWhiteSpace(Options?.DefaultActionText) ? "Open" : Options.DefaultActionText);
            w.SetUInt32(actionsLengthAt, (uint)(w.Length - actionsStart));

            // No hint; the array of dictionary entries is aligned on 8 even when it is empty.
            w.UInt32(0);
            w.Align(8);

            w.Int32(-1);
        }, LinuxCallTimeout);

        DBusMessage answer;
        try
        {
            answer = await reply;
        }
        catch (TimeoutException)
        {
            throw new TimeoutException("The notification server did not answer.");
        }
        catch (InvalidOperationException inner)
        {
            throw new InvalidOperationException($"The notification server refused the notification ({inner.Message}).", inner);
        }

        var serverId = answer.Reader().UInt32();
        lock (_linuxGate)
        {
            if (replaces != 0 && replaces != serverId)
                _linuxIds.Remove(replaces);
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

        DBusConnection? connection;
        lock (_linuxGate) connection = _linuxConnection;
        // With a delay, like the other calls: a bus that does not answer must not keep this call's entry, and its failure is read.
        if (connection is not null)
            Forget(connection.CallAsync(LinuxService, LinuxPath, LinuxService, "CloseNotification", "u", w => w.UInt32(serverId), LinuxCallTimeout));
    }

    private const string BusService = "org.freedesktop.DBus";
    private const string BusPath = "/org/freedesktop/DBus";

    // The unique name of the process that owns the notification service now, or null when there is none (yet). Read when the connection is made,
    // and changed by NameOwnerChanged: it cannot be asked inside a signal, which the thread that reads the replies is running.
    private string? _linuxOwner;

    // A call whose answer is not wanted: a failure (the bus is gone, it did not answer in time) is not left unobserved.
    private static void Forget(Task task) => _ = task.ContinueWith(failed => _ = failed.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    // The notifications of a server that is gone: forget their identifiers and end each one that was waiting as dismissed.
    private void EndServerNotifications()
    {
        uint[] ours;
        lock (_linuxGate)
        {
            ours = [.. _linuxIds.Values];
            _linuxIds.Clear();
            _linuxServerIds.Clear();
        }

        foreach (var id in ours)
            Complete(id, false, null, offThread: true);
    }

    private async Task ReadOwnerAsync(DBusConnection connection)
    {
        try
        {
            var reply = await connection.CallAsync(BusService, BusPath, BusService, "GetNameOwner", "s", w => w.String(LinuxService), LinuxCallTimeout).ConfigureAwait(false);
            lock (_linuxGate)
                _linuxOwner ??= reply.Reader().String(); // a NameOwnerChanged that came first is more recent
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or InvalidDataException or TimeoutException)
        {
            // Nobody owns the service yet: the notification shown starts it, and NameOwnerChanged tells who it is.
        }
    }

    private void OnLinuxSignal(DBusMessage message)
    {
        if (message.Interface == BusService && message.Member == "NameOwnerChanged" && message.Sender == BusService)
        {
            // (name, old_owner, new_owner)
            var change = message.Reader();
            if (message.Signature == "sss" && change.String() == LinuxService)
            {
                var previous = change.String();
                var owner = change.String();
                lock (_linuxGate)
                    _linuxOwner = owner.Length == 0 ? null : owner;
                // The server that gave the identifiers is gone (or was replaced): they mean nothing to the next one, which could even replace another
                // application's bubble with one of them.
                if (previous.Length > 0)
                    EndServerNotifications();
            }

            return;
        }

        if (message.Interface != LinuxService)
            return;

        // From the server and from nobody else, and of the shape the specification gives: ActionInvoked(u id, s action), NotificationClosed(u id, u reason).
        lock (_linuxGate)
        {
            if (_linuxOwner is null || message.Sender != _linuxOwner)
                return;
        }

        var expected = message.Member switch { "ActionInvoked" => "us", "NotificationClosed" => "uu", _ => null };
        if (expected is null || message.Signature != expected)
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
            Complete(id, true, action == "default" ? null : action, offThread: true);
        }
        else if (message.Member == "NotificationClosed")
        {
            lock (_linuxGate)
            {
                _linuxIds.Remove(serverId);
                _linuxServerIds.Remove(id);
            }

            Complete(id, false, null, offThread: true);
        }
    }
}
#endif
