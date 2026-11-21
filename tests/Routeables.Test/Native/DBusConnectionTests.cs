#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS
using System.Net;
using System.Net.Sockets;
using System.Text;
using Routeables.Native.Desktop;

namespace Routeables.Tests.Native;

/// <summary>
/// The reading side of the D-Bus client against a bus that is played by the test: replies, errors and signals sent over a loopback socket, messages that
/// are malformed, and what ends the connection. Nothing here needs a session bus.
/// </summary>
public class DBusConnectionTests
{
    private const byte MethodReturn = 2, ErrorMessage = 3, Signal = 4;
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>A message as the bus writes it: the fixed part, the header fields (each aligned on 8), the padding, then the body.</summary>
    internal static byte[] Frame(byte type, uint serial, Action<DBusWriter> fields, byte[]? body = null)
    {
        body ??= [];
        var writer = new DBusWriter();
        writer.Raw(new byte[16]);
        fields(writer);
        var fieldsLength = writer.Length - 16;
        writer.Align(8);
        writer.Raw(body);
        writer.SetUInt32(4, (uint)body.Length);
        writer.SetUInt32(8, serial);
        writer.SetUInt32(12, (uint)fieldsLength);
        var bytes = writer.ToArray();
        bytes[0] = (byte)'l';
        bytes[1] = type;
        bytes[3] = 1;
        return bytes;
    }

    internal static Action<DBusWriter> ReplyTo(uint serial) => w => { w.Align(8); w.Byte(5); w.Signature("u"); w.UInt32(serial); };

    internal static byte[] Text(string text)
    {
        var writer = new DBusWriter();
        writer.String(text);
        return writer.ToArray();
    }

    private static byte[] SignalFrame(string member, uint serial = 1) => Frame(Signal, serial, w =>
    {
        w.Align(8); w.Byte(2); w.Signature("s"); w.String("org.example.Iface");
        w.Align(8); w.Byte(3); w.Signature("s"); w.String(member);
    });

    private static async Task<(Socket Bus, DBusConnection Connection)> OpenAsync(CancellationToken ct)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await client.ConnectAsync(listener.LocalEndpoint, ct);
        var bus = await listener.AcceptSocketAsync(ct);
        return (bus, DBusConnection.ForTestsReading(client));
    }

    [Fact]
    public async Task The_reply_to_a_call_is_given_to_the_one_who_made_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var (bus, connection) = await OpenAsync(ct);
        using var busLease = bus; using var connectionLease = connection;

        var call = connection.CallAsync("org.example", "/", "org.example.Iface", "Ask", null, null);
        await bus.SendAsync(Frame(MethodReturn, 7, ReplyTo(1), Text("answer")), ct);

        var reply = await call.WaitAsync(Patience, ct);
        Assert.Equal("answer", reply.Reader().String());
        Assert.Equal(0, connection.PendingCount);
    }

    [Fact]
    public async Task The_call_that_is_sent_names_the_method_and_its_destination()
    {
        var ct = TestContext.Current.CancellationToken;
        var (bus, connection) = await OpenAsync(ct);
        using var busLease = bus; using var connectionLease = connection;

        _ = connection.CallAsync("org.example.Dest", "/some/path", "org.example.Iface", "DoIt", "s", w => w.String("arg"));

        var buffer = new byte[512];
        var read = await bus.ReceiveAsync(buffer, ct);
        var text = Encoding.ASCII.GetString(buffer, 0, read);
        Assert.Equal((byte)'l', buffer[0]);
        Assert.Equal(1, buffer[1]);          // a method call
        Assert.Contains("org.example.Dest", text);
        Assert.Contains("/some/path", text);
        Assert.Contains("DoIt", text);
        Assert.Contains("arg", text);
    }

    [Fact]
    public async Task An_error_reply_fails_the_call_with_the_name_of_the_error()
    {
        var ct = TestContext.Current.CancellationToken;
        var (bus, connection) = await OpenAsync(ct);
        using var busLease = bus; using var connectionLease = connection;

        var call = connection.CallAsync("org.example", "/", "org.example.Iface", "Ask", null, null);
        await bus.SendAsync(Frame(ErrorMessage, 7, w =>
        {
            w.Align(8); w.Byte(4); w.Signature("s"); w.String("org.freedesktop.DBus.Error.ServiceUnknown");
            w.Align(8); w.Byte(5); w.Signature("u"); w.UInt32(1);
        }), ct);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => call.WaitAsync(Patience, ct));
        Assert.Contains("org.freedesktop.DBus.Error.ServiceUnknown", error.Message);
        Assert.Equal(0, connection.PendingCount);
    }

    [Fact]
    public async Task A_reply_nobody_waits_for_is_ignored_and_the_next_message_is_still_read()
    {
        var ct = TestContext.Current.CancellationToken;
        var (bus, connection) = await OpenAsync(ct);
        using var busLease = bus; using var connectionLease = connection;
        var got = new TaskCompletionSource<DBusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SignalReceived += m => got.TrySetResult(m);

        await bus.SendAsync(Frame(MethodReturn, 7, ReplyTo(999), Text("late")), ct);
        await bus.SendAsync(SignalFrame("Hello"), ct);

        var signal = await got.Task.WaitAsync(Patience, ct);
        Assert.Equal("Hello", signal.Member);
        Assert.Equal("org.example.Iface", signal.Interface);
    }

    [Fact]
    public async Task A_signal_is_given_to_the_handler_with_its_body()
    {
        var ct = TestContext.Current.CancellationToken;
        var (bus, connection) = await OpenAsync(ct);
        using var busLease = bus; using var connectionLease = connection;
        var got = new TaskCompletionSource<DBusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SignalReceived += m => got.TrySetResult(m);

        await bus.SendAsync(Frame(Signal, 3, w =>
        {
            w.Align(8); w.Byte(3); w.Signature("s"); w.String("Told");
            w.Align(8); w.Byte(8); w.Signature("g"); w.Signature("s");
        }, Text("payload")), ct);

        var signal = await got.Task.WaitAsync(Patience, ct);
        Assert.Equal("s", signal.Signature);
        Assert.Equal("payload", signal.Reader().String());
    }

    [Fact]
    public async Task A_handler_that_throws_does_not_end_the_reading()
    {
        var ct = TestContext.Current.CancellationToken;
        var (bus, connection) = await OpenAsync(ct);
        using var busLease = bus; using var connectionLease = connection;
        var closed = false;
        connection.Closed += () => closed = true;
        var second = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        connection.SignalReceived += m =>
        {
            if (Interlocked.Increment(ref count) == 1)
                throw new InvalidOperationException("the handler fails");
            second.TrySetResult(m.Member);
        };

        await bus.SendAsync(SignalFrame("First"), ct);
        await bus.SendAsync(SignalFrame("Second"), ct);

        Assert.Equal("Second", await second.Task.WaitAsync(Patience, ct));
        Assert.False(closed);
    }

    [Fact]
    public async Task A_malformed_message_is_dropped_and_the_connection_goes_on()
    {
        var ct = TestContext.Current.CancellationToken;
        var (bus, connection) = await OpenAsync(ct);
        using var busLease = bus; using var connectionLease = connection;
        var got = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SignalReceived += m => got.TrySetResult(m.Member);

        // A string of 500 characters that the header does not hold: the lengths are right, the content is not.
        await bus.SendAsync(Frame(Signal, 2, w => { w.Align(8); w.Byte(2); w.Signature("s"); w.UInt32(500); }), ct);
        await bus.SendAsync(SignalFrame("AfterTheBadOne"), ct);

        Assert.Equal("AfterTheBadOne", await got.Task.WaitAsync(Patience, ct));
    }

    [Fact]
    public async Task A_big_endian_message_is_read_whole_and_dropped()
    {
        var ct = TestContext.Current.CancellationToken;
        var (bus, connection) = await OpenAsync(ct);
        using var busLease = bus; using var connectionLease = connection;
        var got = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SignalReceived += m => got.TrySetResult(m.Member);

        var big = new byte[16 + 3];            // no header fields, a body of three bytes, lengths written big endian
        big[0] = (byte)'B';
        big[1] = Signal;
        big[7] = 3;
        await bus.SendAsync(big, ct);
        await bus.SendAsync(SignalFrame("AfterBigEndian"), ct);

        Assert.Equal("AfterBigEndian", await got.Task.WaitAsync(Patience, ct));
    }

    [Fact]
    public async Task What_is_not_a_message_ends_the_connection_and_fails_the_calls_that_wait()
    {
        var ct = TestContext.Current.CancellationToken;
        var (bus, connection) = await OpenAsync(ct);
        using var busLease = bus; using var connectionLease = connection;
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += () => closed.TrySetResult();

        var call = connection.CallAsync("org.example", "/", "org.example.Iface", "Ask", null, null);
        await bus.SendAsync(Enumerable.Repeat((byte)'x', 16).ToArray(), ct);

        await closed.Task.WaitAsync(Patience, ct);
        await Assert.ThrowsAsync<IOException>(() => call.WaitAsync(Patience, ct));
        Assert.Equal(0, connection.PendingCount);
    }

    [Fact]
    public async Task A_bus_that_hangs_up_ends_the_connection()
    {
        var ct = TestContext.Current.CancellationToken;
        var (bus, connection) = await OpenAsync(ct);
        using var connectionLease = connection;
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += () => closed.TrySetResult();

        var call = connection.CallAsync("org.example", "/", "org.example.Iface", "Ask", null, null);
        bus.Dispose();

        await closed.Task.WaitAsync(Patience, ct);
        await Assert.ThrowsAsync<IOException>(() => call.WaitAsync(Patience, ct));
    }

    [Fact]
    public async Task A_message_of_an_impossible_length_ends_the_connection()
    {
        var ct = TestContext.Current.CancellationToken;
        var (bus, connection) = await OpenAsync(ct);
        using var busLease = bus; using var connectionLease = connection;
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += () => closed.TrySetResult();

        var bad = new byte[16];
        bad[0] = (byte)'l';
        bad[1] = Signal;
        BitConverter.GetBytes(uint.MaxValue).CopyTo(bad, 4);       // a body of four billion bytes: not allocated
        await bus.SendAsync(bad, ct);

        await closed.Task.WaitAsync(Patience, ct);
    }

    [Fact]
    public async Task A_call_on_a_connection_that_is_disposed_fails_instead_of_waiting()
    {
        var ct = TestContext.Current.CancellationToken;
        var (bus, connection) = await OpenAsync(ct);
        using var busLease = bus;
        connection.Dispose();

        var call = connection.CallAsync("org.example", "/", "org.example.Iface", "Ask", null, null);

        var error = await Assert.ThrowsAsync<IOException>(() => call.WaitAsync(Patience, ct));
        Assert.Contains("not connected", error.Message);
        Assert.Equal(0, connection.PendingCount);
    }

    [Theory]
    [InlineData("y", 2)]            // read from position 1
    [InlineData("n", 4)]            // aligned on 2, two bytes
    [InlineData("q", 4)]
    [InlineData("b", 8)]            // aligned on 4, four bytes
    [InlineData("i", 8)]
    [InlineData("u", 8)]
    [InlineData("h", 8)]
    [InlineData("x", 16)]           // aligned on 8, eight bytes
    [InlineData("t", 16)]
    [InlineData("d", 16)]
    public void A_basic_value_is_skipped_with_its_alignment(string type, int position)
    {
        var reader = new DBusReader(new byte[16], 1);

        reader.Skip(type);

        Assert.Equal(position, reader.Position);
    }

    [Fact]
    public void Strings_paths_and_signatures_are_skipped_to_the_end_of_their_value()
    {
        var writer = new DBusWriter();
        writer.String("a string");
        writer.String("/a/path");
        writer.Signature("a{sv}");
        var reader = new DBusReader(writer.ToArray());

        reader.Skip("s");
        reader.Skip("o");
        reader.Skip("g");

        Assert.Equal(writer.Length, reader.Position);
    }

    [Fact]
    public void A_variant_is_skipped_with_the_value_it_holds()
    {
        var writer = new DBusWriter();
        writer.Signature("s");
        writer.String("held");
        writer.Byte(9);
        var reader = new DBusReader(writer.ToArray());

        reader.Skip("v");

        Assert.Equal(writer.Length - 1, reader.Position);
        Assert.Equal(9, reader.Byte());
    }

    [Theory]
    [InlineData("ay", 1, 3)]        // the length, then the bytes
    [InlineData("ai", 4, 8)]        // the elements start aligned on 4
    [InlineData("ax", 8, 8)]        // aligned on 8: the length ends at 4, padding to 8, then the elements
    public void An_array_of_numbers_is_skipped_by_its_length_in_bytes(string type, int align, int length)
    {
        var writer = new DBusWriter();
        writer.UInt32((uint)length);
        writer.Align(align);
        writer.Raw(new byte[length]);
        var reader = new DBusReader(writer.ToArray());

        reader.Skip(type);

        Assert.Equal(writer.Length, reader.Position);
    }

    [Fact]
    public void An_array_that_claims_more_than_the_message_holds_is_refused()
    {
        var writer = new DBusWriter();
        writer.UInt32(1000);
        writer.Raw(new byte[2]);

        Assert.Throws<InvalidDataException>(() => new DBusReader(writer.ToArray()).Skip("ay"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("as")]              // an array of strings is not one of the arrays that are read
    [InlineData("(ii)")]
    [InlineData("a{sv}")]
    [InlineData("z")]
    public void A_type_that_is_not_read_is_refused(string type)
        => Assert.Throws<InvalidDataException>(() => new DBusReader(new byte[64]).Skip(type));
}

/// <summary>Tests that change DBUS_SESSION_BUS_ADDRESS: one at a time, and the variable is put back.</summary>
[CollectionDefinition("D-Bus environment", DisableParallelization = true)]
public class DBusEnvironmentCollection;

[Collection("D-Bus environment")]
public class DBusConnectTests
{
    public static bool OnLinux => OperatingSystem.IsLinux();

    private const string Variable = "DBUS_SESSION_BUS_ADDRESS";

    private enum Mode { Accept, RefuseAuthentication, TooLongAnswer, HangUpAfterBegin }

    private sealed class Bus : IDisposable
    {
        private readonly Socket _listener = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "routeables dbus " + Guid.NewGuid().ToString("N")[..8]);
        public string SocketPath { get; }
        public Task<string> Served { get; }
        public string Escaped => "unix:path=" + Uri.EscapeDataString(SocketPath);

        public Bus(Mode mode)
        {
            Directory.CreateDirectory(_directory);
            SocketPath = Path.Combine(_directory, "bus");
            _listener.Bind(new UnixDomainSocketEndPoint(SocketPath));
            _listener.Listen(1);
            Served = Task.Run(() => Serve(mode));
        }

        private static string ReadUntil(Socket socket, string end)
        {
            var text = new StringBuilder();
            var one = new byte[1];
            while (!text.ToString().EndsWith(end, StringComparison.Ordinal))
            {
                if (socket.Receive(one) == 0)
                    break;
                text.Append((char)one[0]);
            }
            return text.ToString();
        }

        private string Serve(Mode mode)
        {
            using var client = _listener.Accept();
            var auth = ReadUntil(client, "\r\n");
            switch (mode)
            {
                case Mode.RefuseAuthentication:
                    client.Send(Encoding.ASCII.GetBytes("REJECTED EXTERNAL\r\n"));
                    return auth;
                case Mode.TooLongAnswer:
                    try { client.Send(Encoding.ASCII.GetBytes(new string('a', 6000))); } catch (SocketException) { }
                    return auth;
            }

            client.Send(Encoding.ASCII.GetBytes("OK 0123456789abcdef\r\n"));
            ReadUntil(client, "BEGIN\r\n");
            if (mode == Mode.HangUpAfterBegin)
                return auth;

            try
            {
                client.Receive(new byte[512]);                      // the call of Hello
                client.Send(DBusConnectionTests.Frame(2, 1, DBusConnectionTests.ReplyTo(1), DBusConnectionTests.Text(":1.7")));
                client.Receive(new byte[16]);                       // until the client hangs up
            }
            catch (SocketException) { }
            return auth;
        }

        public void Dispose()
        {
            _listener.Dispose();
            try { Directory.Delete(_directory, true); } catch (IOException) { }
        }
    }

    private static async Task<T> WithAddress<T>(string? address, Func<Task<T>> body)
    {
        var before = Environment.GetEnvironmentVariable(Variable);
        Environment.SetEnvironmentVariable(Variable, address);
        try { return await body(); }
        finally { Environment.SetEnvironmentVariable(Variable, before); }
    }

    [Fact(Skip = "Needs Linux: the session bus is reached over a Unix socket and libc.", SkipUnless = nameof(OnLinux))]
    public async Task A_bus_that_accepts_the_authentication_and_says_hello_gives_a_connection()
    {
        using var bus = new Bus(Mode.Accept);

        using var connection = await WithAddress(bus.Escaped, DBusConnection.TryConnectAsync);

        Assert.NotNull(connection);
    }

    [Fact(Skip = "Needs Linux: the session bus is reached over a Unix socket and libc.", SkipUnless = nameof(OnLinux))]
    public async Task The_authentication_is_external_with_the_user_id_in_hexadecimal_and_the_path_is_unescaped()
    {
        using var bus = new Bus(Mode.Accept);

        using var connection = await WithAddress(bus.Escaped, DBusConnection.TryConnectAsync);
        connection!.Dispose();

        var auth = await bus.Served.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.StartsWith("\0AUTH EXTERNAL ", auth);
        Assert.EndsWith("\r\n", auth);
        var hex = auth["\0AUTH EXTERNAL ".Length..].TrimEnd('\r', '\n');
        Assert.Matches("^([0-9a-f]{2})+$", hex);
        Assert.All(Encoding.ASCII.GetString(Convert.FromHexString(hex)), c => Assert.True(char.IsDigit(c)));
    }

    [Fact(Skip = "Needs Linux: the session bus is reached over a Unix socket and libc.", SkipUnless = nameof(OnLinux))]
    public async Task Addresses_that_are_not_unix_or_do_not_answer_are_passed_over()
    {
        using var bus = new Bus(Mode.Accept);
        var address = "tcp:host=localhost,port=1;unix:path=" + Uri.EscapeDataString(Path.Combine(Path.GetTempPath(), "routeables-no-such-bus")) + ";" + bus.Escaped;

        using var connection = await WithAddress(address, DBusConnection.TryConnectAsync);

        Assert.NotNull(connection);
    }

    [Fact(Skip = "Needs Linux: the session bus is reached over a Unix socket and libc.", SkipUnless = nameof(OnLinux))]
    public async Task A_bus_that_refuses_the_authentication_gives_no_connection()
    {
        using var bus = new Bus(Mode.RefuseAuthentication);

        var connection = await WithAddress(bus.Escaped, DBusConnection.TryConnectAsync);

        Assert.Null(connection);
    }

    [Fact(Skip = "Needs Linux: the session bus is reached over a Unix socket and libc.", SkipUnless = nameof(OnLinux))]
    public async Task An_answer_that_never_ends_its_line_gives_no_connection()
    {
        using var bus = new Bus(Mode.TooLongAnswer);

        var connection = await WithAddress(bus.Escaped, DBusConnection.TryConnectAsync);

        Assert.Null(connection);
    }

    [Fact(Skip = "Needs Linux: the session bus is reached over a Unix socket and libc.", SkipUnless = nameof(OnLinux))]
    public async Task A_bus_that_hangs_up_before_saying_hello_gives_no_connection()
    {
        using var bus = new Bus(Mode.HangUpAfterBegin);

        var connection = await WithAddress(bus.Escaped, DBusConnection.TryConnectAsync);

        Assert.Null(connection);
    }

    [Fact(Skip = "Needs Linux: the session bus is reached over a Unix socket and libc.", SkipUnless = nameof(OnLinux))]
    public async Task An_address_that_names_nothing_that_exists_gives_no_connection()
    {
        var missing = "unix:path=" + Uri.EscapeDataString(Path.Combine(Path.GetTempPath(), "routeables-no-such-bus-" + Guid.NewGuid().ToString("N")));

        var connection = await WithAddress(missing, DBusConnection.TryConnectAsync);

        Assert.Null(connection);
    }
}
#endif
