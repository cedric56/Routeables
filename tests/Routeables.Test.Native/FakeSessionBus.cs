#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS
using Routeables.Native.Desktop;
using System.Net.Sockets;
using System.Text;

namespace Routeables.Tests.Native;

/// <summary>
/// A session bus and a notification server that the test plays: it listens on a Unix socket, accepts the authentication, answers the calls the client
/// makes (by default as a working server of notifications does) and sends the signals the test asks for. DBUS_SESSION_BUS_ADDRESS points to it
/// until it is disposed, so it is used from the tests of the collection "D-Bus environment".
/// </summary>
internal sealed class FakeSessionBus : IDisposable
{
    public const string Server = ":1.9";            // the unique name of the process that serves the notifications
    public const string Interface = "org.freedesktop.Notifications";

    private readonly Socket _listener = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "routeables bus " + Guid.NewGuid().ToString("N")[..8]);
    private readonly string? _before = Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS");
    private readonly TaskCompletionSource<Socket> _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<DBusMessage> _calls = [];
    private readonly object _gate = new();
    private uint _serial = 100;
    private uint _nextNotification = 41;

    /// <summary>What is answered to a call: the body of the reply, or <see langword="null"/> for the error ServiceUnknown. The default serves like a notification server.</summary>
    public Func<DBusMessage, byte[]?> Answer { get; set; }

    /// <summary>The identifier that the next Notify call is answered with.</summary>
    public uint NextNotificationId { get => _nextNotification; set => _nextNotification = value; }

    public FakeSessionBus()
    {
        Answer = DefaultAnswer;
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "bus");
        _listener.Bind(new UnixDomainSocketEndPoint(path));
        _listener.Listen(1);
        Environment.SetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS", "unix:path=" + Uri.EscapeDataString(path));
        _ = Task.Run(Serve);
    }

    public IReadOnlyList<DBusMessage> Calls { get { lock (_gate) return [.. _calls]; } }

    private byte[]? DefaultAnswer(DBusMessage call) => call.Member switch
    {
        "Hello" => DBusConnectionTests.Text(":1.5"),
        "GetNameOwner" => DBusConnectionTests.Text(Server),
        "GetServerInformation" => Strings("FakeServer", "Routeables", "1.0", "1.2"),
        "Notify" => UInt(_nextNotification++),
        _ => [],        // AddMatch, CloseNotification...
    };

    public static byte[] Strings(params string[] values)
    {
        var writer = new DBusWriter();
        foreach (var value in values)
            writer.String(value);
        return writer.ToArray();
    }

    public static byte[] UInt(uint value)
    {
        var writer = new DBusWriter();
        writer.UInt32(value);
        return writer.ToArray();
    }

    private static byte[] ReadExactly(Socket socket, int count)
    {
        var bytes = new byte[count];
        for (var read = 0; read < count;)
        {
            var got = socket.Receive(bytes, read, count - read, SocketFlags.None);
            if (got == 0)
                throw new IOException("closed");
            read += got;
        }
        return bytes;
    }

    private void Serve()
    {
        try
        {
            using var client = _listener.Accept();
            var line = new StringBuilder();
            var one = new byte[1];
            while (!line.ToString().EndsWith("\r\n", StringComparison.Ordinal))
            {
                client.Receive(one);
                line.Append((char)one[0]);
            }
            client.Send(Encoding.ASCII.GetBytes("OK 0123456789abcdef\r\n"));
            while (!line.ToString().EndsWith("BEGIN\r\n", StringComparison.Ordinal))
            {
                client.Receive(one);
                line.Append((char)one[0]);
            }
            _connected.TrySetResult(client);

            while (true)
            {
                var fixedPart = ReadExactly(client, 16);
                var body = BitConverter.ToUInt32(fixedPart, 4);
                var fields = (int)BitConverter.ToUInt32(fixedPart, 12);
                var padding = (8 - (16 + fields) % 8) % 8;
                var rest = ReadExactly(client, fields + padding + (int)body);
                var header = new byte[16 + fields];
                fixedPart.CopyTo(header, 0);
                Array.Copy(rest, 0, header, 16, fields);
                var call = DBusConnection.ParseMessage(fixedPart[1], BitConverter.ToUInt32(fixedPart, 8), header, rest[(fields + padding)..]);
                lock (_gate) _calls.Add(call);

                var answer = Answer(call);
                byte[] reply = answer is null
                    ? DBusConnectionTests.Frame(3, ++_serial, w =>
                    {
                        w.Align(8); w.Byte(4); w.Signature("s"); w.String("org.freedesktop.DBus.Error.ServiceUnknown");
                        w.Align(8); w.Byte(5); w.Signature("u"); w.UInt32(call.Serial);
                    })
                    : DBusConnectionTests.Frame(2, ++_serial, DBusConnectionTests.ReplyTo(call.Serial), answer);
                lock (client) client.Send(reply);
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException) { }
    }

    /// <summary>Waits until the client has made the <paramref name="count"/>th call of <paramref name="member"/>.</summary>
    public async Task<DBusMessage> CallAsync(string member, int count = 1)
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < until)
        {
            lock (_gate)
            {
                var found = _calls.Where(c => c.Member == member).Skip(count - 1).FirstOrDefault();
                if (found is not null)
                    return found;
            }
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        throw new TimeoutException($"The client never called {member} ({count}).");
    }

    /// <summary>Sends a signal as the bus would, from <paramref name="sender"/>.</summary>
    public async Task SignalAsync(string sender, string @interface, string member, string signature, byte[] body)
    {
        var client = await _connected.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var frame = DBusConnectionTests.Frame(4, ++_serial, w =>
        {
            w.Align(8); w.Byte(7); w.Signature("s"); w.String(sender);
            w.Align(8); w.Byte(2); w.Signature("s"); w.String(@interface);
            w.Align(8); w.Byte(3); w.Signature("s"); w.String(member);
            w.Align(8); w.Byte(8); w.Signature("g"); w.Signature(signature);
        }, body);
        lock (client) client.Send(frame);
    }

    public Task ActionInvokedAsync(uint serverId, string action, string sender = Server)
    {
        var writer = new DBusWriter();
        writer.UInt32(serverId);
        writer.String(action);
        return SignalAsync(sender, Interface, "ActionInvoked", "us", writer.ToArray());
    }

    public Task NotificationClosedAsync(uint serverId, uint reason = 2, string sender = Server)
    {
        var writer = new DBusWriter();
        writer.UInt32(serverId);
        writer.UInt32(reason);
        return SignalAsync(sender, Interface, "NotificationClosed", "uu", writer.ToArray());
    }

    public Task NameOwnerChangedAsync(string previous, string owner)
        => SignalAsync("org.freedesktop.DBus", "org.freedesktop.DBus", "NameOwnerChanged", "sss", Strings(Interface, previous, owner));

    /// <summary>The bus goes away: the client sees its connection end.</summary>
    public async Task HangUpAsync()
    {
        var client = await _connected.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        client.Dispose();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS", _before);
        _listener.Dispose();
        if (_connected.Task.IsCompletedSuccessfully)
            try { _connected.Task.Result.Dispose(); } catch (ObjectDisposedException) { }
        try { Directory.Delete(_directory, true); } catch (IOException) { }
    }
}
#endif
