using Routeables.Test;

namespace Routeables.Tests.Core;

/// <summary>The comparison of screenshots the visual tests of every host share (<see cref="Snapshot"/>).</summary>
public class SnapshotTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "routeables-snapshot-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ROUTEABLES_UPDATE_SNAPSHOTS", null);
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private static byte[] Picture(int width, int height, Func<int, int, (int R, int G, int B)> colour)
    {
        var rgba = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var (r, g, b) = colour(x, y);
                var o = (y * width + x) * 4;
                rgba[o] = (byte)r; rgba[o + 1] = (byte)g; rgba[o + 2] = (byte)b; rgba[o + 3] = 255;
            }
        return Snapshot.Encode(width, height, rgba);
    }

    [Fact]
    public void A_picture_survives_the_encoder_and_the_decoder()
    {
        var rgba = new byte[10 * 7 * 4];
        new Random(1).NextBytes(rgba);

        var (width, height, pixels) = Snapshot.Decode(Snapshot.Encode(10, 7, rgba));

        Assert.Equal((10, 7), (width, height));
        Assert.Equal(rgba, pixels);
    }

    // An RGB PNG written by another encoder (zlib of Python), whose rows use the five filters of the format.
    private const string FilteredPng = "iVBORw0KGgoAAAANSUhEUgAAAAkAAAAGCAIAAACepSOSAAAAs0lEQVR4nAGoAFf/AHlCvfIhBvCEd2Lw88tNdk3HByBRFZoPifLG2gHK40TxTs6KzF0/4itT5hn5sfk+GaeZphnhXNMCVhOzcn8QjVCbEvs6ZVFk8TPiaoNLXPLLiNSRA4UL1LQSaEXJbBlb+7YKbv8BKgxF78JzLwAkjASJJJ+fB6BCxX8Q0mHfDiNxILtGAymU7a50g2kAcYl6p1/eMTSkqnLgViisb+aKcz0RYaFdjq4ri4ROHUmLCkQAAAAASUVORK5CYII=";
    private const string FilteredPixels = "121,66,189,242,33,6,240,132,119,98,240,243,203,77,118,77,199,7,32,81,21,154,15,137,242,198,218,202,227,68,187,49,18,69,253,111,132,223,154,215,197,179,208,118,172,14,143,83,167,53,108,136,145,63,32,246,247,45,176,34,210,77,10,150,218,212,60,22,23,193,169,142,120,18,158,3,39,55,16,101,208,149,134,79,21,173,160,184,70,193,192,235,197,52,138,220,121,154,223,132,155,173,5,212,161,10,192,68,30,170,238,180,180,142,250,11,31,10,189,128,233,152,163,90,186,94,160,189,135,153,193,53,13,67,158,113,137,122,167,95,222,49,52,164,170,114,224,86,40,172,111,230,138,115,61,17,97,161,93,142,174,43";

    [Fact]
    public void A_picture_written_by_another_encoder_with_every_filter_is_read_right()
    {
        var (width, height, pixels) = Snapshot.Decode(Convert.FromBase64String(FilteredPng));
        var rgb = FilteredPixels.Split(',').Select(byte.Parse).ToArray();

        Assert.Equal((9, 6), (width, height));
        for (var i = 0; i < width * height; i++)
        {
            Assert.Equal(rgb[i * 3], pixels[i * 4]);
            Assert.Equal(rgb[i * 3 + 1], pixels[i * 4 + 1]);
            Assert.Equal(rgb[i * 3 + 2], pixels[i * 4 + 2]);
            Assert.Equal(255, pixels[i * 4 + 3]);
        }
    }

    [Fact]
    public void Without_an_image_kept_the_screenshot_is_written_for_a_look_and_the_test_is_skipped()
    {
        var png = Picture(8, 8, (_, _) => (10, 20, 30));

        var (outcome, message) = Snapshot.Compare(_folder, "page", png);

        Assert.Equal(SnapshotOutcome.NoBaseline, outcome);
        Assert.True(File.Exists(Path.Combine(_folder, "_actual", "page.png")));
        Assert.Contains("page.png", message);
    }

    [Fact]
    public void The_same_picture_matches_and_a_few_moved_pixels_are_allowed()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(Path.Combine(_folder, "page.png"), Picture(100, 100, (_, _) => (200, 200, 200)));

        Assert.Equal(SnapshotOutcome.Match, Snapshot.Compare(_folder, "page", Picture(100, 100, (_, _) => (200, 200, 200))).Outcome);
        // 10 of 10 000 pixels (0.1 %) are another colour: the antialiasing of a text, not a change.
        Assert.Equal(SnapshotOutcome.Match, Snapshot.Compare(_folder, "page", Picture(100, 100, (x, y) => y == 0 && x < 10 ? (0, 0, 0) : (200, 200, 200))).Outcome);
        // A slightly different shade everywhere is not a difference either.
        Assert.Equal(SnapshotOutcome.Match, Snapshot.Compare(_folder, "page", Picture(100, 100, (_, _) => (210, 205, 200))).Outcome);
    }

    [Fact]
    public void A_changed_area_fails_and_the_new_screenshot_is_kept_beside()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(Path.Combine(_folder, "page.png"), Picture(100, 100, (_, _) => (200, 200, 200)));

        var (outcome, _) = Snapshot.Compare(_folder, "page", Picture(100, 100, (x, _) => x < 10 ? (0, 0, 0) : (200, 200, 200)));

        Assert.Equal(SnapshotOutcome.Different, outcome);
        Assert.True(File.Exists(Path.Combine(_folder, "_actual", "page.png")));
    }

    [Fact]
    public void Another_size_is_a_difference()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(Path.Combine(_folder, "page.png"), Picture(100, 100, (_, _) => (200, 200, 200)));

        Assert.Equal(SnapshotOutcome.Different, Snapshot.Compare(_folder, "page", Picture(100, 101, (_, _) => (200, 200, 200))).Outcome);
    }

    [Fact]
    public void A_wanted_change_replaces_the_image_when_asked()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(Path.Combine(_folder, "page.png"), Picture(10, 10, (_, _) => (0, 0, 0)));
        var wanted = Picture(10, 10, (_, _) => (255, 255, 255));

        Environment.SetEnvironmentVariable("ROUTEABLES_UPDATE_SNAPSHOTS", "1");
        Assert.Equal(SnapshotOutcome.Updated, Snapshot.Compare(_folder, "page", wanted).Outcome);
        Environment.SetEnvironmentVariable("ROUTEABLES_UPDATE_SNAPSHOTS", null);

        Assert.Equal(SnapshotOutcome.Match, Snapshot.Compare(_folder, "page", wanted).Outcome);
    }
}
