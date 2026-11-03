namespace Routeables.Core;

/// <summary>
/// Provides access to information about the current operating system distribution or version.
/// </summary>
public interface IDistribution
{
    /// <summary>
    /// Gets the current operating system distribution or version.
    /// </summary>
    Distribution Current { get; }
}

/// <summary>
/// Provides a concrete implementation of <see cref="IDistribution"/> that detects the specific 
/// Linux distribution, Windows version, or Android API release level at runtime.
/// </summary>
internal class DistributionImplementation : IDistribution
{
    private static readonly Lazy<Distribution> _current = new(Detect);

    /// <summary>
    /// Gets the detected current operating system distribution or version.
    /// </summary>
    public Distribution Current => _current.Value;

    // The newest release first: the first one the system is at least as recent as is its release.
    private static readonly (int Api, Distribution Release)[] AndroidReleases =
    [
        (36, Distribution.Android16), (35, Distribution.Android15), (34, Distribution.Android14), (33, Distribution.Android13),
        (32, Distribution.Android12L), (31, Distribution.Android12), (30, Distribution.Android11), (29, Distribution.Android10),
        (28, Distribution.Android9), (27, Distribution.Android8_1), (26, Distribution.Android8), (25, Distribution.Android7_1),
        (24, Distribution.Android7), (23, Distribution.Android6), (22, Distribution.Android5_1), (21, Distribution.Android5),
    ];

    private static readonly (int Major, int Minor, int Build, Distribution Release)[] WindowsReleases =
    [
        (10, 0, 22000, Distribution.Windows11), (10, 0, 10240, Distribution.Windows10), (6, 3, 9600, Distribution.Windows81),
        (6, 2, 9200, Distribution.Windows8), (6, 1, 7600, Distribution.Windows7), (6, 0, 0, Distribution.Vista),
        (5, 1, 0, Distribution.WindowsXP),
    ];

    // The first family a Linux system belongs to, by the names its os-release gives as ID or ID_LIKE.
    private static readonly (Distribution Release, string[] Ids)[] LinuxFamilies =
    [
        (Distribution.Ubuntu, ["ubuntu"]), (Distribution.Fedora, ["fedora"]), (Distribution.ArchLinux, ["arch"]),
        (Distribution.Debian, ["debian"]), (Distribution.OpenSUSE, ["opensuse", "opensuse-leap", "opensuse-tumbleweed", "suse"]),
        (Distribution.LinuxMint, ["linuxmint", "mint"]), (Distribution.Manjaro, ["manjaro"]), (Distribution.PopOS, ["pop", "pop_os"]),
    ];

    /// <summary>
    /// Detects the current operating system distribution or version based on the platform.
    /// </summary>
    /// <returns>A <see cref="Distribution"/> value representing the detected OS distribution or version.</returns>
    public static Distribution Detect()
    {
        if (OperatingSystem.IsAndroid())
        {
            foreach (var (api, release) in AndroidReleases)
                if (OperatingSystem.IsAndroidVersionAtLeast(api))
                    return release;
        }

        if (OperatingSystem.IsWindows())
        {
            foreach (var (major, minor, build, release) in WindowsReleases)
                if (OperatingSystem.IsWindowsVersionAtLeast(major, minor, build))
                    return release;
        }

        if (OperatingSystem.IsLinux())
        {
            var osRelease = ReadOsRelease();
            var id = Get(osRelease, "ID");
            var idLike = Get(osRelease, "ID_LIKE");

            foreach (var (release, ids) in LinuxFamilies)
                if (Matches(id, idLike, ids))
                    return release;
        }

        return Distribution.Unknown;
    }

    /// <summary>
    /// Reads and parses the <c>/etc/os-release</c> file on Linux systems into a dictionary of key-value pairs.
    /// </summary>
    /// <returns>A dictionary containing OS release properties.</returns>
    private static Dictionary<string, string> ReadOsRelease()
    {
        const string path = "/etc/os-release";

        if (!File.Exists(path))
            return new Dictionary<string, string>();

        // The last assignment of a key wins, as for the shell that reads the file; a comment is not an assignment.
        var values = new Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.TrimStart().StartsWith('#') || ParseLine(line.Trim()) is not { } pair)
                continue;

            values[pair.Key] = pair.Value;
        }

        return values;
    }

    /// <summary>
    /// Parses a single line from the <c>os-release</c> file.
    /// </summary>
    /// <param name="line">The text line to parse.</param>
    /// <returns>A key-value pair if successfully parsed; otherwise, <see langword="null"/>.</returns>
    private static KeyValuePair<string, string>? ParseLine(string line)
    {
        var separator = line.IndexOf('=');

        if (separator <= 0)
            return null;

        var key = line[..separator];
        var value = line[(separator + 1)..].Trim();

        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'')))
        {
            value = value[1..^1];
        }

        return new(key, value);
    }

    /// <summary>
    /// Retrieves a lowercased value for the specified key from the parsed <c>os-release</c> dictionary.
    /// </summary>
    /// <param name="values">The dictionary of values.</param>
    /// <param name="key">The lookup key.</param>
    /// <returns>The lowercased string value, or an empty string if not found.</returns>
    private static string Get(
        Dictionary<string, string> values,
        string key)
    {
        return values.TryGetValue(key, out var value)
            ? value.ToLowerInvariant()
            : string.Empty;
    }

    /// <summary>
    /// Determines whether the distribution ID or fallback identifiers match any of the target distribution names.
    /// </summary>
    /// <param name="id">The primary OS identifier.</param>
    /// <param name="idLike">The fallback compatible OS identifiers.</param>
    /// <param name="values">An array of target distribution keywords to match against.</param>
    /// <returns><see langword="true"/> if a match is found; otherwise, <see langword="false"/>.</returns>
    private static bool Matches(
        string id,
        string idLike,
        params string[] values)
    {
        var identifiers = $"{id} {idLike}"
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return values.Any(value =>
            identifiers.Contains(
                value,
                StringComparer.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Represents a strongly typed operating system distribution or version identifier.
/// </summary>
public readonly struct Distribution : IEquatable<Distribution>
{
    private readonly string distribution;

    /// <summary>Gets a <see cref="Distribution"/> instance representing Ubuntu.</summary>
    public static Distribution Ubuntu { get; } = new Distribution(nameof(Ubuntu));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Fedora.</summary>
    public static Distribution Fedora { get; } = new Distribution(nameof(Fedora));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Arch Linux.</summary>
    public static Distribution ArchLinux { get; } = new Distribution(nameof(ArchLinux));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Debian.</summary>
    public static Distribution Debian { get; } = new Distribution(nameof(Debian));

    /// <summary>Gets a <see cref="Distribution"/> instance representing openSUSE.</summary>
    public static Distribution OpenSUSE { get; } = new Distribution(nameof(OpenSUSE));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Linux Mint.</summary>
    public static Distribution LinuxMint { get; } = new Distribution(nameof(LinuxMint));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Manjaro.</summary>
    public static Distribution Manjaro { get; } = new Distribution(nameof(Manjaro));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Pop!_OS.</summary>
    public static Distribution PopOS { get; } = new Distribution(nameof(PopOS));

    /// <summary>Gets a <see cref="Distribution"/> instance representing an unknown distribution.</summary>
    public static Distribution Unknown { get; } = new Distribution(nameof(Unknown));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Windows 11.</summary>
    public static Distribution Windows11 { get; } = new Distribution(nameof(Windows11));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Windows 10.</summary>
    public static Distribution Windows10 { get; } = new Distribution(nameof(Windows10));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Windows Vista.</summary>
    public static Distribution Vista { get; } = new Distribution(nameof(Vista));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Windows XP.</summary>
    public static Distribution WindowsXP { get; } = new Distribution(nameof(WindowsXP));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Windows 8.1.</summary>
    public static Distribution Windows81 { get; } = new Distribution(nameof(Windows81));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Windows 8.</summary>
    public static Distribution Windows8 { get; } = new Distribution(nameof(Windows8));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Windows 7.</summary>
    public static Distribution Windows7 { get; } = new Distribution(nameof(Windows7));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Android 16.</summary>
    public static Distribution Android16 { get; } = new Distribution(nameof(Android16));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Android 15.</summary>
    public static Distribution Android15 { get; } = new Distribution(nameof(Android15));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Android 14.</summary>
    public static Distribution Android14 { get; } = new Distribution(nameof(Android14));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Android 13.</summary>
    public static Distribution Android13 { get; } = new Distribution(nameof(Android13));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Android 12L.</summary>
    public static Distribution Android12L { get; } = new Distribution(nameof(Android12L));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Android 12.</summary>
    public static Distribution Android12 { get; } = new Distribution(nameof(Android12));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Android 11.</summary>
    public static Distribution Android11 { get; } = new Distribution(nameof(Android11));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Android 10.</summary>
    public static Distribution Android10 { get; } = new Distribution(nameof(Android10));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Android 9.</summary>
    public static Distribution Android9 { get; } = new Distribution(nameof(Android9));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Android 8.1.</summary>
    public static Distribution Android8_1 { get; } = new Distribution(nameof(Android8_1));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Android 8.</summary>
    public static Distribution Android8 { get; } = new Distribution(nameof(Android8));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Android 7.1.</summary>
    public static Distribution Android7_1 { get; } = new Distribution(nameof(Android7_1));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Android 7.</summary>
    public static Distribution Android7 { get; } = new Distribution(nameof(Android7));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Android 6.</summary>
    public static Distribution Android6 { get; } = new Distribution(nameof(Android6));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Android 5.1.</summary>
    public static Distribution Android5_1 { get; } = new Distribution(nameof(Android5_1));

    /// <summary>Gets a <see cref="Distribution"/> instance representing Android 5.</summary>
    public static Distribution Android5 { get; } = new Distribution(nameof(Android5));

    /// <summary>
    /// Initializes a new instance of the <see cref="Distribution"/> struct with the specified identifier string.
    /// </summary>
    /// <param name="distribution">The distribution platform identifier.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="distribution"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="distribution"/> is empty.</exception>
    internal Distribution(string distribution)
    {
        if (distribution == null)
            throw new ArgumentNullException(nameof(distribution));

        if (distribution.Length == 0)
            throw new ArgumentException("Distribution identifier cannot be empty.", nameof(distribution));

        this.distribution = distribution;
    }

    /// <summary>
    /// Creates a new device platform instance. This can be used to define custom platforms.
    /// </summary>
    /// <param name="distribution">The device platform identifier.</param>
    /// <returns>A new instance of <see cref="Distribution"/> with the specified platform identifier.</returns>
    public static Distribution Create(string distribution) =>
        new Distribution(distribution);

    /// <summary>
    /// Determines whether the current <see cref="Distribution"/> instance is equal to another <see cref="Distribution"/> instance.
    /// </summary>
    /// <param name="other">The <see cref="Distribution"/> object to compare with.</param>
    /// <returns><see langword="true"/> if they are equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(Distribution other) =>
        Equals(other.distribution);

    /// <summary>
    /// Determines whether the current distribution identifier matches a string value using ordinal comparison.
    /// </summary>
    /// <param name="other">The string value to compare with.</param>
    /// <returns><see langword="true"/> if they are equal; otherwise, <see langword="false"/>.</returns>
    internal bool Equals(string other) =>
        string.Equals(distribution, other, StringComparison.Ordinal);

    /// <inheritdoc cref="IEquatable{T}.Equals(T)"/>
    public override bool Equals(object? obj) =>
        obj is Distribution && Equals((Distribution)obj);

    /// <summary>
    /// Gets the hash code for this platform instance.
    /// </summary>
    /// <returns>The computed hash code for this device platform, or <c>0</c> when uninitialized.</returns>
    public override int GetHashCode() =>
        distribution == null ? 0 : distribution.GetHashCode(
#if !NETSTANDARD2_0
            StringComparison.Ordinal
#endif
        );

    /// <summary>
    /// Returns a string representation of the current distribution platform identifier.
    /// </summary>
    /// <returns>A string representation of this instance, or an empty string when not set.</returns>
    public override string ToString() =>
        distribution ?? string.Empty;

    /// <summary>
    /// Determines whether two specified <see cref="Distribution"/> instances have the same value.
    /// </summary>
    /// <param name="left">The first distribution instance to compare.</param>
    /// <param name="right">The second distribution instance to compare.</param>
    /// <returns><see langword="true"/> if the objects are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(Distribution left, Distribution right) =>
        left.Equals(right);

    /// <summary>
    /// Determines whether two specified <see cref="Distribution"/> instances have different values.
    /// </summary>
    /// <param name="left">The first distribution instance to compare.</param>
    /// <param name="right">The second distribution instance to compare.</param>
    /// <returns><see langword="true"/> if the objects are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(Distribution left, Distribution right) =>
        !left.Equals(right);
}