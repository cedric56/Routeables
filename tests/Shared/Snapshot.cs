using System.IO.Compression;

namespace Routeables.Test;

/// <summary>What <see cref="Snapshot.Compare"/> found.</summary>
public enum SnapshotOutcome
{
    /// <summary>The screenshot is the image kept (or differs by less than the share allowed).</summary>
    Match,

    /// <summary>No image is kept yet: the screenshot was written to <c>_actual</c> to be looked at, and the test should be skipped.</summary>
    NoBaseline,

    /// <summary>The screenshot differs: it was written to <c>_actual</c>, and the test should fail.</summary>
    Different,

    /// <summary>The image kept was replaced because <c>ROUTEABLES_UPDATE_SNAPSHOTS=1</c>.</summary>
    Updated,
}

/// <summary>
/// The comparison of a screenshot with the image kept in the <c>Snapshots</c> folder of a test project, the same for every host that draws
/// one (Avalonia, WPF, MAUI, Uno): the host gives a PNG, this compares it. Written without an image library, to be linked into any test
/// project whatever its framework. The images of a platform are those of its renderer and fonts: make them with CI, not on your own machine.
/// <list type="bullet">
/// <item>No image yet: the screenshot goes to <c>Snapshots/_actual</c> and the outcome is <see cref="SnapshotOutcome.NoBaseline"/>. If the image is right, move it to <c>Snapshots</c> (CI uploads <c>_actual</c> as an artifact).</item>
/// <item>A difference: <see cref="SnapshotOutcome.Different"/>, and the new screenshot goes to <c>Snapshots/_actual</c>.</item>
/// <item>A change that is wanted: <c>ROUTEABLES_UPDATE_SNAPSHOTS=1</c> replaces the images.</item>
/// </list>
/// </summary>
public static class Snapshot
{
    /// <summary>The share of pixels allowed to differ: the antialiasing of the text moves a few.</summary>
    public const double AllowedDifference = 0.002;

    // A pixel differs when one of its channels moves by more than this (of 255).
    private const int ChannelDelta = 48;

    /// <summary>The <c>Snapshots</c> folder of the test project whose project file is <paramref name="project"/>, found above the output folder.</summary>
    public static string FolderOf(string project)
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, project)))
            root = Path.GetDirectoryName(root.TrimEnd(Path.DirectorySeparatorChar));
        return Path.Combine(root ?? throw new InvalidOperationException($"The folder of {project} was not found above {AppContext.BaseDirectory}."), "Snapshots");
    }

    /// <summary>Compares <paramref name="png"/> with <c>name.png</c> of <paramref name="folder"/>.</summary>
    /// <returns>The outcome, and what to tell the person who reads the result.</returns>
    public static (SnapshotOutcome Outcome, string Message) Compare(string folder, string name, byte[] png)
    {
        var baseline = Path.Combine(folder, name + ".png");
        var actual = Path.Combine(folder, "_actual", name + ".png");

        if (Environment.GetEnvironmentVariable("ROUTEABLES_UPDATE_SNAPSHOTS") == "1")
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(baseline, png);
            return (SnapshotOutcome.Updated, $"{name}: the image was replaced.");
        }

        if (!File.Exists(baseline))
        {
            Write(actual, png);
            return (SnapshotOutcome.NoBaseline, $"{name}: no image kept yet. Look at {actual}; if it is right, move it to {baseline}.");
        }

        var expected = Decode(File.ReadAllBytes(baseline));
        var seen = Decode(png);
        if (expected.Width != seen.Width || expected.Height != seen.Height)
        {
            Write(actual, png);
            return (SnapshotOutcome.Different, $"{name}: the screenshot is {seen.Width}x{seen.Height}, the image kept {expected.Width}x{expected.Height}. See {actual}.");
        }

        var different = 0L;
        for (var i = 0; i < expected.Pixels.Length; i += 4)
        {
            if (Math.Abs(expected.Pixels[i] - seen.Pixels[i]) > ChannelDelta
                || Math.Abs(expected.Pixels[i + 1] - seen.Pixels[i + 1]) > ChannelDelta
                || Math.Abs(expected.Pixels[i + 2] - seen.Pixels[i + 2]) > ChannelDelta
                || Math.Abs(expected.Pixels[i + 3] - seen.Pixels[i + 3]) > ChannelDelta)
                different++;
        }

        var share = different / (double)(expected.Width * expected.Height);
        if (share <= AllowedDifference)
            return (SnapshotOutcome.Match, $"{name}: {share:P3} of the pixels differ.");

        Write(actual, png);
        return (SnapshotOutcome.Different, $"{name}: {share:P2} of the pixels differ (more than {AllowedDifference:P2}). See {actual}.");
    }

    private static void Write(string path, byte[] png)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, png);
    }

    // ------------------------------------------------------------------------------------------------ PNG

    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>Writes RGBA pixels (4 bytes each, row after row) as a PNG.</summary>
    public static byte[] Encode(int width, int height, byte[] rgba)
    {
        if (rgba.Length != width * height * 4)
            throw new ArgumentException("The pixels do not fit the size.", nameof(rgba));

        using var raw = new MemoryStream();
        using (var zlib = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (var y = 0; y < height; y++)
            {
                zlib.WriteByte(0);                                    // filter: none
                zlib.Write(rgba, y * width * 4, width * 4);
            }
        }

        using var png = new MemoryStream();
        png.Write(Signature);
        var header = new byte[13];
        WriteInt(header, 0, width);
        WriteInt(header, 4, height);
        header[8] = 8;      // bits per channel
        header[9] = 6;      // RGBA
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", raw.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    /// <summary>Reads a PNG of 8 bits per channel, grey, RGB or RGBA, not interlaced (what the encoders of the hosts write), as RGBA pixels.</summary>
    public static (int Width, int Height, byte[] Pixels) Decode(byte[] png)
    {
        if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(Signature))
            throw new InvalidDataException("Not a PNG.");

        int width = 0, height = 0, type = 0;
        var data = new MemoryStream();
        byte[]? palette = null;
        for (var at = 8; at + 8 <= png.Length;)
        {
            var length = ReadInt(png, at);
            var kind = System.Text.Encoding.ASCII.GetString(png, at + 4, 4);
            var body = png.AsSpan(at + 8, length);
            switch (kind)
            {
                case "IHDR":
                    width = ReadInt(png, at + 8);
                    height = ReadInt(png, at + 12);
                    if (png[at + 16] != 8 || png[at + 20] != 0)
                        throw new InvalidDataException("Only PNGs of 8 bits per channel, not interlaced, are read.");
                    type = png[at + 17];
                    break;
                case "PLTE": palette = body.ToArray(); break;
                case "IDAT": data.Write(body); break;
            }
            at += 12 + length;
            if (kind == "IEND") break;
        }

        var channels = type switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new InvalidDataException($"PNG colour type {type} is not read.") };
        data.Position = 0;
        var stride = width * channels;
        var lines = new byte[height * (stride + 1)];
        using (var zlib = new ZLibStream(data, CompressionMode.Decompress))
            zlib.ReadExactly(lines);

        var pixels = new byte[width * height * 4];
        var previous = new byte[stride];
        var line = new byte[stride];
        for (var y = 0; y < height; y++)
        {
            var filter = lines[y * (stride + 1)];
            Array.Copy(lines, y * (stride + 1) + 1, line, 0, stride);
            for (var x = 0; x < stride; x++)
            {
                int a = x >= channels ? line[x - channels] : 0, b = previous[x], c = x >= channels ? previous[x - channels] : 0;
                line[x] = (byte)(line[x] + filter switch
                {
                    0 => 0,
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => Paeth(a, b, c),
                    _ => throw new InvalidDataException($"PNG filter {filter} is not known."),
                });
            }

            for (var x = 0; x < width; x++)
            {
                var o = (y * width + x) * 4;
                var p = x * channels;
                switch (type)
                {
                    case 0: pixels[o] = pixels[o + 1] = pixels[o + 2] = line[p]; pixels[o + 3] = 255; break;
                    case 2: pixels[o] = line[p]; pixels[o + 1] = line[p + 1]; pixels[o + 2] = line[p + 2]; pixels[o + 3] = 255; break;
                    case 3:
                        var entry = line[p] * 3;
                        pixels[o] = palette![entry]; pixels[o + 1] = palette[entry + 1]; pixels[o + 2] = palette[entry + 2]; pixels[o + 3] = 255;
                        break;
                    case 4: pixels[o] = pixels[o + 1] = pixels[o + 2] = line[p]; pixels[o + 3] = line[p + 1]; break;
                    default: pixels[o] = line[p]; pixels[o + 1] = line[p + 1]; pixels[o + 2] = line[p + 2]; pixels[o + 3] = line[p + 3]; break;
                }
            }

            (previous, line) = (line, previous);
        }

        return (width, height, pixels);
    }

    /// <summary>Turns the BGRA pixels most renderers give (premultiplied or not, the colour channels are compared as they are) into RGBA.</summary>
    public static byte[] BgraToRgba(byte[] bgra)
    {
        var rgba = new byte[bgra.Length];
        for (var i = 0; i < bgra.Length; i += 4)
        {
            rgba[i] = bgra[i + 2];
            rgba[i + 1] = bgra[i + 1];
            rgba[i + 2] = bgra[i];
            rgba[i + 3] = bgra[i + 3];
        }
        return rgba;
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static int ReadInt(byte[] bytes, int at) => (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];

    private static void WriteInt(byte[] bytes, int at, int value)
    {
        bytes[at] = (byte)(value >> 24);
        bytes[at + 1] = (byte)(value >> 16);
        bytes[at + 2] = (byte)(value >> 8);
        bytes[at + 3] = (byte)value;
    }

    private static void Chunk(Stream png, string kind, byte[] body)
    {
        var head = new byte[8];
        WriteInt(head, 0, body.Length);
        System.Text.Encoding.ASCII.GetBytes(kind, head.AsSpan(4));
        png.Write(head);
        png.Write(body);

        var crc = Crc(head.AsSpan(4), body);
        var tail = new byte[4];
        WriteInt(tail, 0, unchecked((int)crc));
        png.Write(tail);
    }

    private static uint Crc(ReadOnlySpan<byte> kind, ReadOnlySpan<byte> body)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var part in new[] { kind.ToArray(), body.ToArray() })
            foreach (var b in part)
            {
                crc ^= b;
                for (var k = 0; k < 8; k++)
                    crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        return ~crc;
    }
}
