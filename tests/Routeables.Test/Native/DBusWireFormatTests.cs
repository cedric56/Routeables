#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS
using Routeables.Native.Desktop;

namespace Routeables.Tests.Native;

/// <summary>
/// The wire format of the D-Bus (little endian, aligned from the start of the buffer) that the Linux notifications speak.
/// It is computation only: it is checked against the bytes the specification gives, without a bus.
/// </summary>
public class DBusWireFormatTests
{
    [Fact]
    public void A_string_is_its_length_the_utf8_text_and_a_zero()
    {
        var writer = new DBusWriter();
        writer.String("hi");

        Assert.Equal(new byte[] { 2, 0, 0, 0, (byte)'h', (byte)'i', 0 }, writer.ToArray());
    }

    [Fact]
    public void A_signature_is_its_length_on_one_byte_the_text_and_a_zero()
    {
        var writer = new DBusWriter();
        writer.Signature("su");

        Assert.Equal(new byte[] { 2, (byte)'s', (byte)'u', 0 }, writer.ToArray());
    }

    [Fact]
    public void A_uint32_is_little_endian_and_aligned_on_four()
    {
        var writer = new DBusWriter();
        writer.Byte(1);
        writer.UInt32(0x01020304);

        Assert.Equal(new byte[] { 1, 0, 0, 0, 4, 3, 2, 1 }, writer.ToArray());
    }

    [Fact]
    public void An_int32_is_written_as_its_two_complement_uint32()
    {
        var writer = new DBusWriter();
        writer.Int32(-1);

        Assert.Equal(new byte[] { 255, 255, 255, 255 }, writer.ToArray());
    }

    [Fact]
    public void Align_pads_with_zeros_and_does_nothing_when_already_aligned()
    {
        var writer = new DBusWriter();
        writer.Byte(9);
        writer.Align(8);
        var padded = writer.Length;
        writer.Align(8);

        Assert.Equal(8, padded);
        Assert.Equal(8, writer.Length);
        Assert.Equal(new byte[] { 9, 0, 0, 0, 0, 0, 0, 0 }, writer.ToArray());
    }

    [Fact]
    public void SetUInt32_patches_a_length_written_before_its_content()
    {
        var writer = new DBusWriter();
        writer.UInt32(0);
        writer.Raw([7, 7, 7]);
        writer.SetUInt32(0, 3);

        Assert.Equal(new byte[] { 3, 0, 0, 0, 7, 7, 7 }, writer.ToArray());
    }

    [Fact]
    public void What_is_written_is_read_back_in_the_same_order()
    {
        var writer = new DBusWriter();
        writer.Byte(42);
        writer.String("Routeables");
        writer.UInt32(123456789);
        writer.Signature("a{sv}");
        writer.String("");
        writer.String("é ✓ 日本");

        var reader = new DBusReader(writer.ToArray());

        Assert.Equal(42, reader.Byte());
        Assert.Equal("Routeables", reader.String());
        Assert.Equal(123456789u, reader.UInt32());
        Assert.Equal("a{sv}", reader.Signature());
        Assert.Equal("", reader.String());
        Assert.Equal("é ✓ 日本", reader.String());
        Assert.Equal(writer.Length, reader.Position);
    }

    [Fact]
    public void The_length_of_a_string_counts_bytes_not_characters()
    {
        var writer = new DBusWriter();
        writer.String("é");

        Assert.Equal(new byte[] { 2, 0, 0, 0, 0xC3, 0xA9, 0 }, writer.ToArray());
    }

    [Fact]
    public void A_message_reads_its_body_from_the_start()
    {
        var writer = new DBusWriter();
        writer.String("body");
        var message = new DBusMessage { Body = writer.ToArray() };

        Assert.Equal("body", message.Reader().String());
    }

    [Fact]
    public void A_reader_can_start_after_a_header_and_align_from_the_start_of_the_buffer()
    {
        var writer = new DBusWriter();
        writer.Byte(1);
        writer.UInt32(77);
        var reader = new DBusReader(writer.ToArray(), 1);

        Assert.Equal(77u, reader.UInt32());
    }

    // --- what a peer sends is not trusted ---

    [Fact]
    public void Reading_past_the_end_is_refused_not_an_ArgumentOutOfRange()
    {
        Assert.Throws<InvalidDataException>(() => new DBusReader([]).Byte());
        Assert.Throws<InvalidDataException>(() => new DBusReader([1, 0]).UInt32());
        Assert.Throws<InvalidDataException>(() => new DBusReader([]).UInt32());
        Assert.Throws<InvalidDataException>(() => new DBusReader([5, (byte)'s']).Signature());
    }

    [Fact]
    public void A_string_that_claims_more_than_the_message_holds_is_refused()
    {
        var writer = new DBusWriter();
        writer.UInt32(uint.MaxValue);          // four billion characters: not allocated, not read
        writer.Raw([(byte)'a', 0]);

        Assert.Throws<InvalidDataException>(() => new DBusReader(writer.ToArray()).String());
    }

    [Fact]
    public void A_string_without_its_zero_is_refused()
    {
        var writer = new DBusWriter();
        writer.UInt32(2);
        writer.Raw([(byte)'h', (byte)'i']);    // the terminating zero is missing

        Assert.Throws<InvalidDataException>(() => new DBusReader(writer.ToArray()).String());
    }

    [Theory]
    [InlineData(uint.MaxValue, 0u)]
    [InlineData(0u, uint.MaxValue)]
    [InlineData(0x8000_0000u, 0u)]             // negative as an int: the cast must not come first
    [InlineData(DBusConnection.MaxMessageLength, 1u)]
    [InlineData(DBusConnection.MaxMessageLength - 20, 16u)]
    public void The_lengths_of_a_message_above_what_the_specification_allows_are_refused(uint body, uint fields)
        => Assert.Throws<InvalidOperationException>(() => DBusConnection.ValidateLengths(body, fields));

    [Fact]
    public void The_lengths_of_an_ordinary_message_are_kept()
        => Assert.Equal((12, 40), DBusConnection.ValidateLengths(12, 40));

    private static byte[] Header(Action<DBusWriter> fields)
    {
        var writer = new DBusWriter();
        writer.Raw(new byte[16]);              // the fixed part: the fields start at 16, which is aligned on 8
        fields(writer);
        return writer.ToArray();
    }

    [Fact]
    public void The_sender_that_the_bus_writes_in_the_header_is_read()
    {
        var header = Header(w =>
        {
            w.Byte(7); w.Signature("s"); w.String(":1.42");       // SENDER
            w.Align(8);
            w.Byte(2); w.Signature("s"); w.String("org.freedesktop.Notifications");   // INTERFACE
            w.Align(8);
            w.Byte(3); w.Signature("s"); w.String("ActionInvoked"); // MEMBER
            w.Align(8);
            w.Byte(8); w.Signature("g"); w.Signature("us");        // SIGNATURE
        });

        var message = DBusConnection.ParseMessage(4, 9, header, []);

        Assert.Equal(":1.42", message.Sender);
        Assert.Equal("org.freedesktop.Notifications", message.Interface);
        Assert.Equal("ActionInvoked", message.Member);
        Assert.Equal("us", message.Signature);
    }

    [Fact]
    public void A_message_without_a_sender_has_none()
    {
        var header = Header(w => { w.Byte(3); w.Signature("s"); w.String("Anything"); });

        Assert.Null(DBusConnection.ParseMessage(4, 1, header, []).Sender);
    }

    [Fact]
    public void A_header_that_is_cut_short_or_has_a_field_nobody_knows_is_refused()
    {
        var cut = Header(w => { w.Byte(2); w.Signature("s"); w.UInt32(500); });     // a string of 500 characters that is not there
        var unknown = Header(w => { w.Byte(9); w.Signature("x"); w.Raw(new byte[8]); });

        Assert.Throws<InvalidDataException>(() => DBusConnection.ParseMessage(4, 1, cut, []));
        Assert.Throws<InvalidOperationException>(() => DBusConnection.ParseMessage(4, 1, unknown, []));
    }

    [Fact]
    public void A_body_that_is_empty_is_refused_when_it_is_read_not_before()
    {
        var message = new DBusMessage { Body = [] };

        // What the notification client does with a signal that has no body: the read fails with a refusal it can tell, which the reading thread catches.
        Assert.Throws<InvalidDataException>(() => message.Reader().UInt32());
    }
}
#endif
