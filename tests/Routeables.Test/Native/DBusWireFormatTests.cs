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
}
#endif
