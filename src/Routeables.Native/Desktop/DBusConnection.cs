#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
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

    /// <summary>The unique name of the sender, which the bus itself writes in the header: a peer cannot choose it.</summary>
    public string? Sender { get; init; }
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
        Span<byte> little = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(little, value); // the messages are marked little-endian whatever the machine
        foreach (var b in little) _bytes.Add(b);
    }

    public void Int32(int value) => UInt32(unchecked((uint)value));

    public void SetUInt32(int position, uint value)
    {
        Span<byte> little = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(little, value);
        for (var i = 0; i < 4; i++)
            _bytes[position + i] = little[i];
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
        // The length is one byte: a longer signature would be written with a wrong length and corrupt the message.
        if (value.Length > byte.MaxValue)
            throw new ArgumentException("A D-Bus signature is 255 characters at most.", nameof(value));
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

    // What a peer sends is not trusted: a message that is cut short or that lies about a length is rejected (InvalidDataException), never read
    // past its end, and never allocated at the size it claims.
    private void Need(long count)
    {
        if (count < 0 || Position + count > bytes.Length)
            throw new InvalidDataException("The D-Bus message is shorter than it says.");
    }

    public byte Byte()
    {
        Need(1);
        return bytes[Position++];
    }

    public uint UInt32()
    {
        Align(4);
        Need(4);
        var value = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(Position, 4));
        Position += 4;
        return value;
    }

    public string String()
    {
        var length = UInt32();
        Need((long)length + 1); // the text and its zero terminator
        var text = Encoding.UTF8.GetString(bytes, Position, (int)length);
        Position += (int)length + 1;
        return text;
    }

    public string Signature()
    {
        var length = Byte();
        Need(length + 1);
        var text = Encoding.ASCII.GetString(bytes, Position, length);
        Position += length + 1;
        return text;
    }

    /// <summary>
    /// Reads past one value of the given type without keeping it: the specification asks a reader to ignore the header fields that it does not know.
    /// Basic types, variants and arrays of basic types are known; a structure or a dictionary is refused (<see cref="InvalidDataException"/>).
    /// </summary>
    public void Skip(string type) => Skip(type, 0);

    // The specification bounds the nesting of variants (a variant holds one value, which may itself be a variant), so the recursion is bounded
    // too: a stack overflow cannot be caught.
    private const int MaxVariantDepth = 32;

    private void Skip(string type, int depth)
    {
        if (type.Length == 0)
            throw new InvalidDataException("An empty D-Bus type.");
        if (type[0] != 'a' && type.Length != 1)
            throw new InvalidDataException("A D-Bus value has one complete type: " + type);

        switch (type[0])
        {
            case 'y': Need(1); Position += 1; break;
            case 'n' or 'q': Align(2); Need(2); Position += 2; break;
            case 'b' or 'i' or 'u' or 'h': Align(4); Need(4); Position += 4; break;
            case 'x' or 't' or 'd': Align(8); Need(8); Position += 8; break;
            case 's' or 'o': String(); break;
            case 'g': Signature(); break;
            case 'v':
                if (depth >= MaxVariantDepth)
                    throw new InvalidDataException("D-Bus variants nested too deep.");
                Skip(Signature(), depth + 1);
                break;
            case 'a' when type.Length == 2 && type[1] is 'y' or 'n' or 'q' or 'b' or 'i' or 'u' or 'h' or 'x' or 't' or 'd':
                var length = UInt32();
                Align(type[1] switch { 'y' => 1, 'n' or 'q' => 2, 'b' or 'i' or 'u' or 'h' => 4, _ => 8 });
                Need(length);
                Position += (int)length;
                break;
            default:
                throw new InvalidDataException("A D-Bus type that is not read: " + type);
        }
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

    // What the specification allows: a message is at most 128 MiB (the header fields 64 MiB, which the total includes).
    internal const uint MaxMessageLength = 128 * 1024 * 1024;
    private const int MaxAuthLine = 4096;

    private readonly Socket _socket;
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<DBusMessage>> _pending = new();
    private readonly object _writeGate = new();
    private uint _serial;

    private DBusConnection(Socket socket) => _socket = socket;

    /// <summary>A connection over a socket that is already connected, without the authentication or the reading thread (for the tests).</summary>
    internal static DBusConnection ForTests(Socket socket) => new(socket);

    /// <summary>A connection over a socket that is already connected, with the reading thread but without the authentication or Hello (for the tests of what the bus sends back).</summary>
    internal static DBusConnection ForTestsReading(Socket socket)
    {
        var connection = new DBusConnection(socket);
        new Thread(connection.ReadLoop) { IsBackground = true, Name = "Routeables D-Bus (test)" }.Start();
        return connection;
    }

    /// <summary>The calls that wait for a reply (for the tests).</summary>
    internal int PendingCount => _pending.Count;

    /// <summary>Raised on the reading thread for each signal.</summary>
    public event Action<DBusMessage>? SignalReceived;

    /// <summary>Raised when the connection ends.</summary>
    public event Action? Closed;

    /// <summary>Connects to the session bus and says Hello to it; <see langword="null"/> when there is no session bus (a server, a container).</summary>
    public static async Task<DBusConnection?> TryConnectAsync()
    {
        foreach (var endpoint in Endpoints())
        {
            try
            {
                return await ConnectAsync(endpoint);
            }
            catch (Exception ex) when (IsConnectFailure(ex))
            {
                // The next endpoint, if there is one.
            }
        }

        return null;
    }

    private static bool IsConnectFailure(Exception ex)
        => ex is SocketException or IOException or OperationCanceledException or InvalidOperationException or InvalidDataException or TimeoutException;

    // Connects, authenticates, starts the reading thread and says Hello; the socket is closed when any of it fails.
    private static async Task<DBusConnection> ConnectAsync(EndPoint endpoint)
    {
        Socket? socket = null;
        try
        {
            socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await socket.ConnectAsync(endpoint, timeout.Token);

            // The authentication is synchronous: a bus that does not answer must not hold a thread forever. The reading thread waits without a limit.
            socket.ReceiveTimeout = socket.SendTimeout = 3000;
            Authenticate(socket);
            socket.ReceiveTimeout = socket.SendTimeout = 0;

            var connection = new DBusConnection(socket);
            new Thread(connection.ReadLoop) { IsBackground = true, Name = "Routeables D-Bus" }.Start();
            // A bus that accepts the authentication and then says nothing must not hold the connection forever.
            await connection.CallAsync("org.freedesktop.DBus", "/org/freedesktop/DBus", "org.freedesktop.DBus", "Hello", null, null, TimeSpan.FromSeconds(3));
            return connection;
        }
        catch (Exception ex) when (IsConnectFailure(ex))
        {
            socket?.Dispose();
            throw;
        }
    }

    // The values of a D-Bus address escape what is not an optionally-escaped character as %hh.
    private static string Unescaped(string value) => Uri.UnescapeDataString(value);

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
                    yield return new UnixDomainSocketEndPoint(Unescaped(pair[5..]));
                }
                else if (pair.StartsWith("abstract=", StringComparison.Ordinal))
                {
                    found = true;
                    yield return new UnixDomainSocketEndPoint("\0" + Unescaped(pair[9..]));
                }
            }
        }

        // The usual place of the session bus when the variable is not set (a service started outside the session).
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
        // Read one byte at a time (what follows the line belongs to the messages), but check for the end of the line where it can be: the last
        // two characters. A bus's answer is a few dozen bytes, so a line that goes on is not a bus.
        var text = new StringBuilder();
        var one = new byte[1];
        while (text.Length < 2 || text[^2] != '\r' || text[^1] != '\n')
        {
            if (text.Length >= MaxAuthLine)
                throw new InvalidDataException("The answer of the session bus to the authentication is too long.");
            if (socket.Receive(one) == 0)
                throw new IOException("The session bus closed the connection.");
            text.Append((char)one[0]);
        }

        return text.ToString().TrimEnd('\r', '\n');
    }

    /// <summary>Calls a method; the task ends with the reply, or fails with the error of the bus.</summary>
    /// <param name="destination">The bus name of the service that is called.</param>
    /// <param name="path">The object path on that service.</param>
    /// <param name="interface">The interface of the method.</param>
    /// <param name="member">The name of the method.</param>
    /// <param name="signature">The signature of the body, or <see langword="null"/> for a call without arguments.</param>
    /// <param name="writeBody">Writes the arguments, or <see langword="null"/> when there are none.</param>
    /// <param name="timeout">
    /// How long to wait for the reply: past it the task fails with a <see cref="TimeoutException"/> and the call is forgotten (a reply that
    /// comes later is dropped), so a bus that does not answer does not keep an entry for each call. <see langword="null"/> waits for ever.
    /// </param>
    public Task<DBusMessage> CallAsync(string destination, string path, string @interface, string member, string? signature, Action<DBusWriter>? writeBody,
        TimeSpan? timeout = null)
    {
        var body = new DBusWriter();
        writeBody?.Invoke(body);

        var source = new TaskCompletionSource<DBusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[] message;
        lock (_writeGate)
        {
            var serial = ++_serial;
            if (serial == 0) serial = ++_serial; // 0 is not a serial: the counter wrapped
            _pending[serial] = source;
            if (timeout is { } wait)
                ForgetAfter(serial, source, member, wait);

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

    // Past the wait the call fails and is forgotten, and the timer is released with the call.
    private void ForgetAfter(uint serial, TaskCompletionSource<DBusMessage> source, string member, TimeSpan wait)
    {
        var timer = new CancellationTokenSource(wait);
        timer.Token.Register(() =>
        {
            _pending.TryRemove(serial, out _);
            source.TrySetException(new TimeoutException($"The session bus did not answer {member} in {wait.TotalSeconds:0.#} s."));
        });
        _ = source.Task.ContinueWith(_ => timer.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
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
                if (fixedPart[0] == (byte)'B')
                {
                    DropBigEndianMessage(fixedPart);
                    continue;
                }
                if (fixedPart[0] != (byte)'l')
                    throw new InvalidOperationException("Not a D-Bus message: the connection is closed.");

                var message = ReadLittleEndianMessage(fixedPart);
                if (message is not null)
                    Dispatch(message);
            }
        }
        catch (Exception ex)
        {
            // Whatever ends the reading ends the connection, and is reported: nothing is thrown on this thread, where nobody could catch it.
            foreach (var pair in _pending)
                if (_pending.TryRemove(pair.Key, out var source))
                    source.TrySetException(new IOException("The session bus connection ended.", ex));

            Closed?.Invoke();
        }
    }

    // A big-endian message is not understood, but its lengths are readable: read it whole and drop it, and the connection goes on.
    private void DropBigEndianMessage(byte[] fixedPart)
    {
        var (skipBody, skipFields) = ValidateLengths(
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(fixedPart.AsSpan(4, 4)),
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(fixedPart.AsSpan(12, 4)));
        _ = ReadExactly(skipFields + (8 - (16 + skipFields) % 8) % 8 + skipBody);
        Trace.TraceWarning("Routeables: a big-endian D-Bus message was dropped.");
    }

    // Reads the rest of a message from its fixed part; null for one that cannot be understood.
    private DBusMessage? ReadLittleEndianMessage(byte[] fixedPart)
    {
        var type = fixedPart[1];
        var serial = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(fixedPart.AsSpan(8, 4));
        var (bodyLength, fieldsLength) = ValidateLengths(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(fixedPart.AsSpan(4, 4)), System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(fixedPart.AsSpan(12, 4)));
        var headerEnd = 16 + fieldsLength;
        var padding = (8 - headerEnd % 8) % 8;

        var rest = ReadExactly(fieldsLength + padding + bodyLength);
        var header = new byte[16 + fieldsLength];
        Array.Copy(fixedPart, header, 16);
        Array.Copy(rest, 0, header, 16, fieldsLength);
        var body = rest[(fieldsLength + padding)..];

        // The message is whole, so what follows it is still readable: one that cannot be understood is dropped, and the connection goes on.
        try
        {
            return ParseMessage(type, serial, header, body);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
        {
            Trace.TraceWarning($"Routeables: a malformed D-Bus message was dropped: {ex.Message}");
            return null;
        }
    }

    /// <summary>The lengths of a message, from the fixed part of its header: refused when a peer claims more than the specification allows.</summary>
    internal static (int BodyLength, int FieldsLength) ValidateLengths(uint bodyLength, uint fieldsLength)
    {
        // As unsigned numbers: casting to int first would turn a huge length into a negative one.
        if (bodyLength > MaxMessageLength || fieldsLength > MaxMessageLength || (ulong)bodyLength + fieldsLength + 16 + 8 > MaxMessageLength)
            throw new InvalidOperationException("A D-Bus message is longer than the 128 MiB the specification allows.");
        return ((int)bodyLength, (int)fieldsLength);
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

    internal static DBusMessage ParseMessage(byte type, uint serial, byte[] header, byte[] body)
    {
        uint replySerial = 0;
        string? @interface = null, member = null, errorName = null, signature = null, sender = null;

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
                    else if (code == 7) sender = text;
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
                    // A field this client does not know is skipped, as the specification asks: the message is still delivered.
                    r.Skip(kind);
                    break;
            }
        }

        return new DBusMessage
        {
            Type = type,
            Serial = serial,
            ReplySerial = replySerial,
            Interface = @interface,
            Member = member,
            ErrorName = errorName,
            Signature = signature,
            Sender = sender,
            Body = body
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
                // A handler that throws must not end the thread that reads the bus (and the application with it, since nobody catches there).
                try { SignalReceived?.Invoke(message); }
                catch (Exception ex) { Trace.TraceWarning($"Routeables: a D-Bus signal could not be handled: {ex}"); }
                break;
        }
    }

    public void Dispose() => _socket.Dispose();
}
#endif
