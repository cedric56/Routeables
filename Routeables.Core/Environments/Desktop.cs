namespace Routeables.Core;

/// <summary>
/// Provides access to information about the current desktop environment or operating system platform.
/// </summary>
public interface IDesktop
{
    /// <summary>
    /// Gets the current desktop environment or platform.
    /// </summary>
    Desktop Current { get; }
}

/// <summary>
/// Provides a concrete implementation of <see cref="IDesktop"/> that detects the operating system 
/// and Linux desktop environment at runtime using environment variables and system checks.
/// </summary>
internal class DesktopImplementation : IDesktop
{
    private readonly Lazy<Desktop> _current = new(Detect);

    /// <summary>
    /// Gets the detected current desktop environment or platform.
    /// </summary>
    public Desktop Current => _current.Value;

    /// <summary>
    /// Detects the current operating system or Linux desktop environment.
    /// </summary>
    /// <returns>A <see cref="Desktop"/> value representing the detected platform.</returns>
    private static Desktop Detect()
    {
        if (OperatingSystem.IsAndroid()) return Desktop.Android;
        if (OperatingSystem.IsIOS()) return Desktop.iOS;
        if (OperatingSystem.IsTvOS()) return Desktop.TvOS;
        if (OperatingSystem.IsWatchOS()) return Desktop.WatchOS;
        if (OperatingSystem.IsWindows()) return Desktop.Windows;
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst()) return Desktop.Macos;
        if (OperatingSystem.IsLinux())
        {
            var xdgDesktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "";
            var desktopSession = Environment.GetEnvironmentVariable("DESKTOP_SESSION") ?? "";
            var combined = $"{xdgDesktop} {desktopSession}".ToUpperInvariant();

            if (combined.Contains("GNOME")) return Desktop.Gnome;
            if (combined.Contains("KDE")) return Desktop.KDE;
            if (combined.Contains("XFCE")) return Desktop.Xfce;
            if (combined.Contains("MATE")) return Desktop.Mate;
            if (combined.Contains("CINNAMON")) return Desktop.Cinnamon;
            if (combined.Contains("UNITY")) return Desktop.Unity;
            if (combined.Contains("LXDE")) return Desktop.LXDE;
            if (IsWsl()) return Desktop.WSL;
        }
        return Desktop.Unknown;
    }

    /// <summary>
    /// Determines whether the current environment is running under Windows Subsystem for Linux (WSL).
    /// </summary>
    /// <returns><see langword="true"/> if running under WSL; otherwise, <see langword="false"/>.</returns>
    private static bool IsWsl()
    {
        // Fast check: Environment variable automatically injected by WSL
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WSL_DISTRO_NAME")) ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WSL_INTEROP")))
        {
            return true;
        }

        // Fallback check: Read Linux kernel OS release info
        try
        {
            if (File.Exists("/proc/sys/kernel/osrelease"))
            {
                var versionText = File.ReadAllText("/proc/sys/kernel/osrelease");
                if (versionText.Contains("microsoft", StringComparison.OrdinalIgnoreCase) ||
                    versionText.Contains("wsl", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignore I/O or permission errors
        }

        return false;
    }
}

/// <summary>
/// Represents a strongly typed desktop environment or operating system platform identifier.
/// </summary>
public readonly struct Desktop : IEquatable<Desktop>
{
    private readonly string desktop;

    /// <summary>Gets a <see cref="Desktop"/> instance that represents the GNOME desktop environment.</summary>
    public static Desktop Gnome { get; } = new Desktop(nameof(Gnome));

    /// <summary>Gets a <see cref="Desktop"/> instance that represents an unknown platform.</summary>
    public static Desktop Unknown { get; } = new Desktop(nameof(Unknown));

    /// <summary>Gets a <see cref="Desktop"/> instance that represents the KDE desktop environment.</summary>
    public static Desktop KDE { get; } = new Desktop(nameof(KDE));

    /// <summary>Gets a <see cref="Desktop"/> instance that represents the XFCE desktop environment.</summary>
    public static Desktop Xfce { get; } = new Desktop(nameof(Xfce));

    /// <summary>Gets a <see cref="Desktop"/> instance that represents the MATE desktop environment.</summary>
    public static Desktop Mate { get; } = new Desktop(nameof(Mate));

    /// <summary>Gets a <see cref="Desktop"/> instance that represents the Cinnamon desktop environment.</summary>
    public static Desktop Cinnamon { get; } = new Desktop(nameof(Cinnamon));

    /// <summary>Gets a <see cref="Desktop"/> instance that represents Windows Subsystem for Linux (WSL).</summary>
    public static Desktop WSL { get; } = new Desktop(nameof(WSL));

    /// <summary>Gets a <see cref="Desktop"/> instance that represents macOS.</summary>
    public static Desktop Macos { get; } = new Desktop(nameof(Macos));

    /// <summary>Gets a <see cref="Desktop"/> instance that represents Windows.</summary>
    public static Desktop Windows { get; } = new Desktop(nameof(Windows));

    /// <summary>Gets a <see cref="Desktop"/> instance that represents iOS.</summary>
    public static Desktop iOS { get; } = new Desktop(nameof(iOS));

    /// <summary>Gets a <see cref="Desktop"/> instance that represents Android.</summary>
    public static Desktop Android { get; } = new Desktop(nameof(Android));

    /// <summary>Gets a <see cref="Desktop"/> instance that represents the Unity desktop environment.</summary>
    public static Desktop Unity { get; } = new Desktop(nameof(Unity));

    /// <summary>Gets a <see cref="Desktop"/> instance that represents the LXDE desktop environment.</summary>
    public static Desktop LXDE { get; } = new Desktop(nameof(LXDE));

    /// <summary>Gets a <see cref="Desktop"/> instance that represents tvOS.</summary>
    public static Desktop TvOS { get; } = new Desktop(nameof(TvOS));

    /// <summary>Gets a <see cref="Desktop"/> instance that represents watchOS.</summary>
    public static Desktop WatchOS { get; } = new Desktop(nameof(WatchOS));

    /// <summary>
    /// Initializes a new instance of the <see cref="Desktop"/> struct with the specified identifier string.
    /// </summary>
    /// <param name="desktop">The desktop platform identifier.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="desktop"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="desktop"/> is empty.</exception>
    internal Desktop(string desktop)
    {
        if (desktop == null)
            throw new ArgumentNullException(nameof(desktop));

        if (desktop.Length == 0)
            throw new ArgumentException("Desktop identifier cannot be empty.", nameof(desktop));

        this.desktop = desktop;
    }

    /// <summary>
    /// Creates a new device platform instance. This can be used to define custom platforms.
    /// </summary>
    /// <param name="desktop">The device platform identifier.</param>
    /// <returns>A new instance of <see cref="Desktop"/> with the specified platform identifier.</returns>
    public static Desktop Create(string desktop) =>
        new Desktop(desktop);

    /// <summary>
    /// Determines whether the current <see cref="Desktop"/> instance is equal to another <see cref="Desktop"/> instance.
    /// </summary>
    /// <param name="other">The <see cref="Desktop"/> object to compare with.</param>
    /// <returns><see langword="true"/> if they are equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(Desktop other) =>
        Equals(other.desktop);

    /// <summary>
    /// Determines whether the current desktop identifier matches a string value using ordinal comparison.
    /// </summary>
    /// <param name="other">The string value to compare with.</param>
    /// <returns><see langword="true"/> if they are equal; otherwise, <see langword="false"/>.</returns>
    internal bool Equals(string other) =>
        string.Equals(desktop, other, StringComparison.Ordinal);

    /// <inheritdoc cref="IEquatable{T}.Equals(T)"/>
    public override bool Equals(object? obj) =>
        obj is Desktop && Equals((Desktop)obj);

    /// <summary>
    /// Gets the hash code for this platform instance.
    /// </summary>
    /// <returns>The computed hash code for this device platform, or <c>0</c> when uninitialized.</returns>
    public override int GetHashCode() =>
        desktop == null ? 0 : desktop.GetHashCode(
#if !NETSTANDARD2_0
            StringComparison.Ordinal
#endif
        );

    /// <summary>
    /// Returns a string representation of the current desktop platform identifier.
    /// </summary>
    /// <returns>A string representation of this instance, or an empty string when not set.</returns>
    public override string ToString() =>
        desktop ?? string.Empty;

    /// <summary>
    /// Determines whether two specified <see cref="Desktop"/> instances have the same value.
    /// </summary>
    /// <param name="left">The first desktop instance to compare.</param>
    /// <param name="right">The second desktop instance to compare.</param>
    /// <returns><see langword="true"/> if the objects are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(Desktop left, Desktop right) =>
        left.Equals(right);

    /// <summary>
    /// Determines whether two specified <see cref="Desktop"/> instances have different values.
    /// </summary>
    /// <param name="left">The first desktop instance to compare.</param>
    /// <param name="right">The second desktop instance to compare.</param>
    /// <returns><see langword="true"/> if the objects are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(Desktop left, Desktop right) =>
        !left.Equals(right);
}