#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace Routeables.Native.Desktop;

/// <summary>A message of the D-Bus: a reply, an error or a signal (only what the notification client reads).</summary>
internal sealed class DBusMessage
{
    public byte Type { get; init; }
    public uint Serial { get; init; }
    public uint ReplySerial { get; init; }
    public string? Interface { get; init; }
    public string? Member { get; init; }
    public string? ErrorName { get; init; }
    public string? Signature { get; init; }
    public byte[] Body { get; init; } = [];

    public DBusReader Reader() => new(Body);
}

/// <summary>Writes the wire format of the D-Bus (little endian), aligned from the start of the buffer.</summary>
internal sealed class DBusWriter
{
    private readonly List<byte> _bytes = [];

    public int Length => _bytes.Count;

    public byte[] ToArray() => [.. _bytes];

    public void Align(int boundary)
    {
        while (_bytes.Count % boundary != 0)
            _bytes.Add(0);
    }

    public void Byte(byte value) => _bytes.Add(value);

    public void UInt32(uint value)
    {
        Align(4);
        _bytes.AddRange(BitConverter.GetBytes(value));
    }

    public void Int32(int value) => UInt32(unchecked((uint)value));

    public void SetUInt32(int position, uint value)
    {
        var bytes = BitConverter.GetBytes(value);
        for (var i = 0; i < 4; i++)
            _bytes[position + i] = bytes[i];
    }

    /// <summary>A string or an object path: its length, the text, a zero.</summary>
    public void String(string value)
    {
        var utf8 = Encoding.UTF8.GetBytes(value);
        UInt32((uint)utf8.Length);
        _bytes.AddRange(utf8);
        _bytes.Add(0);
    }

    /// <summary>A signature: its length on one byte, the text, a zero.</summary>
    public void Signature(string value)
    {
        _bytes.Add((byte)value.Length);
        _bytes.AddRange(Encoding.ASCII.GetBytes(value));
        _bytes.Add(0);
    }

    public void Raw(byte[] bytes) => _bytes.AddRange(bytes);
}

/// <summary>Reads the wire format of the D-Bus (little endian).</summary>
internal sealed class DBusReader(byte[] bytes, int position = 0)
{
    public int Position { get; private set; } = position;

    public void Align(int boundary)
    {
        while (Position % boundary != 0)
            Position++;
    }

    public byte Byte() => bytes[Position++];

    public uint UInt32()
    {
        Align(4);
        var value = BitConverter.ToUInt32(bytes, Position);
        Position += 4;
        return value;
    }

    public string String()
    {
        var length = (int)UInt32();
        var text = Encoding.UTF8.GetString(bytes, Position, length);
        Position += length + 1;
        return text;
    }

    public string Signature()
    {
        var length = Byte();
        var text = Encoding.ASCII.GetString(bytes, Position, length);
        Position += length + 1;
        return text;
    }
}

/// <summary>
/// A minimal client of the D-Bus session bus (Linux): the authentication, method calls and the signals, over a Unix socket, with no library. It is what
/// the notifications need, not a general client. Every call is asynchronous; a dedicated thread reads the replies and the signals.
/// </summary>
internal sealed class DBusConnection : IDisposable
{
    private const byte MethodCall = 1;
    private const byte MethodReturn = 2;
    private const byte ErrorMessage = 3;
    private const byte Signal = 4;

    private readonly Socket _socket;
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<DBusMessage>> _pending = new();
    private readonly object _writeGate = new();
    private uint _serial;

    private DBusConnection(Socket socket) => _socket = socket;

    /// <summary>Raised on the reading thread for each signal.</summary>
    public event Action<DBusMessage>? SignalReceived;

    /// <summary>Raised when the connection ends.</summary>
    public event Action? Closed;

    /// <summary>Connects to the session bus and says Hello to it; <see langword="null"/> when there is no session bus (a server, a container).</summary>
    public static async Task<DBusConnection?> TryConnectAsync()
    {
        foreach (var endpoint in Endpoints())
        {
            Socket? socket = null;
            try
            {
                socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await socket.ConnectAsync(endpoint, timeout.Token);
                Authenticate(socket);

                var connection = new DBusConnection(socket);
                new Thread(connection.ReadLoop) { IsBackground = true, Name = "Routeables D-Bus" }.Start();
                await connection.CallAsync("org.freedesktop.DBus", "/org/freedesktop/DBus", "org.freedesktop.DBus", "Hello", null, null);
                return connection;
            }
            catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException or InvalidOperationException)
            {
                socket?.Dispose();
            }
        }

        return null;
    }

    private static IEnumerable<EndPoint> Endpoints()
    {
        var found = false;
        foreach (var address in (Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS") ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!address.StartsWith("unix:", StringComparison.Ordinal))
                continue;

            foreach (var pair in address[5..].Split(','))
            {
                if (pair.StartsWith("path=", StringComparison.Ordinal))
                {
                    found = true;
                    yield return new UnixDomainSocketEndPoint(pair[5..]);
                }
                else if (pair.StartsWith("abstract=", StringComparison.Ordinal))
                {
                    found = true;
                    yield return new UnixDomainSocketEndPoint("\0" + pair[9..]);
                }
            }
        }

        // The usual place of the bus of the session when the variable is not set (a service started outside the session).
        if (!found)
            yield return new UnixDomainSocketEndPoint($"/run/user/{getuid()}/bus");
    }

    [DllImport("libc", EntryPoint = "getuid")]
    private static extern uint getuid();

    private static void Authenticate(Socket socket)
    {
        var hexUid = Convert.ToHexString(Encoding.ASCII.GetBytes(getuid().ToString(System.Globalization.CultureInfo.InvariantCulture))).ToLowerInvariant();
        socket.Send(Encoding.ASCII.GetBytes("\0AUTH EXTERNAL " + hexUid + "\r\n"));

        var line = ReadLine(socket);
        if (!line.StartsWith("OK", StringComparison.Ordinal))
            throw new InvalidOperationException("The session bus refused the authentication: " + line);

        socket.Send(Encoding.ASCII.GetBytes("BEGIN\r\n"));
    }

    private static string ReadLine(Socket socket)
    {
        var text = new StringBuilder();
        var one = new byte[1];
        while (!text.ToString().EndsWith("\r\n", StringComparison.Ordinal))
        {
            if (socket.Receive(one) == 0)
                throw new IOException("The session bus closed the connection.");
            text.Append((char)one[0]);
        }

        return text.ToString().TrimEnd('\r', '\n');
    }

    /// <summary>Calls a method; the task ends with the reply, or fails with the error of the bus.</summary>
    public Task<DBusMessage> CallAsync(string destination, string path, string @interface, string member, string? signature, Action<DBusWriter>? writeBody)
    {
        var body = new DBusWriter();
        writeBody?.Invoke(body);

        var source = new TaskCompletionSource<DBusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[] message;
        lock (_writeGate)
        {
            var serial = ++_serial;
            _pending[serial] = source;
            message = BuildMethodCall(serial, destination, path, @interface, member, signature, body.ToArray());
            try
            {
                _socket.Send(message);
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
            {
                _pending.TryRemove(serial, out _);
                source.TrySetException(new IOException("The session bus is not connected.", ex));
            }
        }

        return source.Task;
    }

    private static byte[] BuildMethodCall(uint serial, string destination, string path, string @interface, string member, string? signature, byte[] body)
    {
        var w = new DBusWriter();
        w.Byte((byte)'l');
        w.Byte(MethodCall);
        w.Byte(0);
        w.Byte(1);
        w.UInt32((uint)body.Length);
        w.UInt32(serial);

        var lengthAt = w.Length;
        w.UInt32(0);
        var start = w.Length;

        void Field(byte code, char type, string value)
        {
            w.Align(8);
            w.Byte(code);
            w.Signature(type.ToString());
            w.String(value);
        }

        Field(1, 'o', path);
        Field(2, 's', @interface);
        Field(3, 's', member);
        Field(6, 's', destination);
        if (signature is not null)
        {
            w.Align(8);
            w.Byte(8);
            w.Signature("g");
            w.Signature(signature);
        }

        w.SetUInt32(lengthAt, (uint)(w.Length - start));
        w.Align(8);
        w.Raw(body);
        return w.ToArray();
    }

    private void ReadLoop()
    {
        try
        {
            while (true)
            {
                var fixedPart = ReadExactly(16);
                if (fixedPart[0] != (byte)'l')
                    throw new InvalidOperationException("Only little-endian messages are read.");

                var type = fixedPart[1];
                var bodyLength = (int)BitConverter.ToUInt32(fixedPart, 4);
                var serial = BitConverter.ToUInt32(fixedPart, 8);
                var fieldsLength = (int)BitConverter.ToUInt32(fixedPart, 12);
                var headerEnd = 16 + fieldsLength;
                var padding = (8 - headerEnd % 8) % 8;

                var rest = ReadExactly(fieldsLength + padding + bodyLength);
                var header = new byte[16 + fieldsLength];
                Array.Copy(fixedPart, header, 16);
                Array.Copy(rest, 0, header, 16, fieldsLength);
                var body = rest[(fieldsLength + padding)..];

                Dispatch(ParseMessage(type, serial, header, body));
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or InvalidOperationException)
        {
            foreach (var pair in _pending)
                if (_pending.TryRemove(pair.Key, out var source))
                    source.TrySetException(new IOException("The session bus connection ended.", ex));

            Closed?.Invoke();
        }
    }

    private byte[] ReadExactly(int count)
    {
        var buffer = new byte[count];
        var read = 0;
        while (read < count)
        {
            var n = _socket.Receive(buffer, read, count - read, SocketFlags.None);
            if (n == 0)
                throw new IOException("The session bus closed the connection.");
            read += n;
        }

        return buffer;
    }

    private static DBusMessage ParseMessage(byte type, uint serial, byte[] header, byte[] body)
    {
        uint replySerial = 0;
        string? @interface = null, member = null, errorName = null, signature = null;

        var r = new DBusReader(header, 16);
        while (r.Position < header.Length)
        {
            r.Align(8);
            if (r.Position >= header.Length)
                break;

            var code = r.Byte();
            var kind = r.Signature();
            switch (kind)
            {
                case "o":
                case "s":
                    var text = r.String();
                    if (code == 2) @interface = text;
                    else if (code == 3) member = text;
                    else if (code == 4) errorName = text;
                    break;
                case "g":
                    var sig = r.Signature();
                    if (code == 8) signature = sig;
                    break;
                case "u":
                    var number = r.UInt32();
                    if (code == 5) replySerial = number;
                    break;
                default:
                    throw new InvalidOperationException("Unknown header field " + kind);
            }
        }

        return new DBusMessage
        {
            Type = type, Serial = serial, ReplySerial = replySerial, Interface = @interface, Member = member,
            ErrorName = errorName, Signature = signature, Body = body
        };
    }

    private void Dispatch(DBusMessage message)
    {
        switch (message.Type)
        {
            case MethodReturn when _pending.TryRemove(message.ReplySerial, out var ok):
                ok.TrySetResult(message);
                break;
            case ErrorMessage when _pending.TryRemove(message.ReplySerial, out var failed):
                failed.TrySetException(new InvalidOperationException("D-Bus error " + message.ErrorName));
                break;
            case Signal:
                SignalReceived?.Invoke(message);
                break;
        }
    }

    public void Dispose() => _socket.Dispose();
}
#endif
