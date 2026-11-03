namespace Routeables.Core;

/// <summary>
/// Provides access to information about the current application runtime or host framework environment.
/// </summary>
public interface IRuntime
{
    /// <summary>
    /// Gets the current application runtime or host framework environment.
    /// </summary>
    Runtime Current { get; }
}

/// <summary>
/// Provides a concrete implementation of <see cref="IRuntime"/> that holds the current runtime environment.
/// </summary>
/// <param name="runtime">The current runtime value.</param>
internal class RuntimeImplementation(Runtime runtime) : IRuntime
{
    /// <summary>
    /// Gets the current application runtime or host framework environment.
    /// </summary>
    public Runtime Current { get; } = runtime;
}

/// <summary>
/// Represents a strongly typed application runtime or host framework environment identifier.
/// </summary>
public readonly struct Runtime : IEquatable<Runtime>
{
    private readonly string runtime;

    /// <summary>Gets a <see cref="Runtime"/> instance representing the Avalonia framework.</summary>
    public static Runtime Avalonia { get; } = new Runtime(nameof(Avalonia));

    /// <summary>Gets a <see cref="Runtime"/> instance representing Uno Platform.</summary>
    public static Runtime Uno { get; } = new Runtime(nameof(Uno));

    /// <summary>Gets a <see cref="Runtime"/> instance representing WPF.</summary>
    public static Runtime Wpf { get; } = new Runtime(nameof(Wpf));

    /// <summary>Gets a <see cref="Runtime"/> instance representing .NET MAUI.</summary>
    public static Runtime Maui { get; } = new Runtime(nameof(Maui));

    /// <summary>Gets a <see cref="Runtime"/> instance representing .NET MAUI Hybrid (Blazor WebView).</summary>
    public static Runtime MauiHybrid { get; } = new Runtime(nameof(MauiHybrid));

    /// <summary>Gets a <see cref="Runtime"/> instance representing Blazor Server.</summary>
    public static Runtime BlazorServer { get; } = new Runtime(nameof(BlazorServer));

    /// <summary>Gets a <see cref="Runtime"/> instance representing Blazor WebAssembly.</summary>
    public static Runtime BlazorWebAssembly { get; } = new Runtime(nameof(BlazorWebAssembly));

    /// <summary>Gets a <see cref="Runtime"/> instance representing an unknown runtime.</summary>
    public static Runtime Unknown { get; } = new Runtime(nameof(Unknown));

    /// <summary>
    /// Initializes a new instance of the <see cref="Runtime"/> struct with the specified identifier string.
    /// </summary>
    /// <param name="runtime">The runtime identifier.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="runtime"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="runtime"/> is empty.</exception>
    internal Runtime(string runtime)
    {
        if (runtime == null)
            throw new ArgumentNullException(nameof(runtime));

        if (runtime.Length == 0)
            throw new ArgumentException("Runtime identifier cannot be empty.", nameof(runtime));

        this.runtime = runtime;
    }

    /// <summary>
    /// Creates a new device platform instance. This can be used to define custom platforms.
    /// </summary>
    /// <param name="runtime">The device platform identifier.</param>
    /// <returns>A new instance of <see cref="Runtime"/> with the specified platform identifier.</returns>
    public static Runtime Create(string runtime) =>
        new Runtime(runtime);

    /// <summary>
    /// Determines whether the current <see cref="Runtime"/> instance is equal to another <see cref="Runtime"/> instance.
    /// </summary>
    /// <param name="other">The <see cref="Runtime"/> object to compare with.</param>
    /// <returns><see langword="true"/> if they are equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(Runtime other) =>
        Equals(other.runtime);

    /// <summary>
    /// Determines whether the current runtime identifier matches a string value using ordinal comparison.
    /// </summary>
    /// <param name="other">The string value to compare with.</param>
    /// <returns><see langword="true"/> if they are equal; otherwise, <see langword="false"/>.</returns>
    internal bool Equals(string other) =>
        string.Equals(runtime, other, StringComparison.Ordinal);

    /// <inheritdoc cref="IEquatable{T}.Equals(T)"/>
    public override bool Equals(object? obj) =>
        obj is Runtime && Equals((Runtime)obj);

    /// <summary>
    /// Gets the hash code for this platform instance.
    /// </summary>
    /// <returns>The computed hash code for this device platform, or <c>0</c> when uninitialized.</returns>
    public override int GetHashCode() =>
        runtime == null ? 0 : runtime.GetHashCode(
#if !NETSTANDARD2_0
            StringComparison.Ordinal
#endif
        );

    /// <summary>
    /// Returns a string representation of the current runtime platform identifier.
    /// </summary>
    /// <returns>A string representation of this instance, or an empty string when not set.</returns>
    public override string ToString() =>
        runtime ?? string.Empty;

    /// <summary>
    /// Determines whether two specified <see cref="Runtime"/> instances have the same value.
    /// </summary>
    /// <param name="left">The first runtime instance to compare.</param>
    /// <param name="right">The second runtime instance to compare.</param>
    /// <returns><see langword="true"/> if the objects are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(Runtime left, Runtime right) =>
        left.Equals(right);

    /// <summary>
    /// Determines whether two specified <see cref="Runtime"/> instances have different values.
    /// </summary>
    /// <param name="left">The first runtime instance to compare.</param>
    /// <param name="right">The second runtime instance to compare.</param>
    /// <returns><see langword="true"/> if the objects are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(Runtime left, Runtime right) =>
        !left.Equals(right);
}