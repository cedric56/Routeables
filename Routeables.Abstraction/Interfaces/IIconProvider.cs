namespace Routeables.Abstraction;

/// <summary>
/// Gives a page its icon from a key (<see cref="IRouteableEntry.Path"/>). Implement it to draw icons from your own set, a font or
/// resources, and register it with <see cref="IconResolver.Register"/>; the first resolver that knows a key wins.
/// </summary>
public interface IIconResolver
{
    /// <summary>The icon for <paramref name="key"/> as something the host puts in a view (a menu entry, a header).</summary>
    /// <returns>The icon, or <see langword="null"/> when this resolver does not know the key.</returns>
    object? GetIcon(string key);

    /// <summary>The icon for <paramref name="key"/> as an image source, for the hosts that draw it themselves (the MAUI shell).</summary>
    /// <returns>The source, or <see langword="null"/> when this resolver does not know the key.</returns>
    object? GetSource(string key);
}

/// <summary>
/// A list of icon resolvers, safe to use from several threads: a resolver registered twice is kept once,
/// <see cref="Register"/> returns a token that removes it. An application that wants its icons apart from the process-wide
/// ones (tests, several hosts) creates its own and gives it to its views with <see cref="IRouteableEntry.Icons"/>.
/// </summary>
public sealed class IconResolverRegistry : IIconResolver
{
    private readonly object _gate = new();
    private IIconResolver[] _providers = [];

    /// <summary>Adds <paramref name="provider"/> after the ones already registered; the same instance is kept once.</summary>
    /// <returns>A token: disposing it removes the provider again.</returns>
    public IDisposable Register(IIconResolver provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        lock (_gate)
        {
            if (!_providers.Any(p => ReferenceEquals(p, provider)))
                _providers = [.. _providers, provider];
        }

        return new Registration(this, provider);
    }

    public object? GetIcon(string key)
    {
        foreach (var provider in Volatile.Read(ref _providers))
        {
            var icon = provider.GetIcon(key);
            if (icon != null) return icon;
        }
        return null;
    }

    public object? GetSource(string key)
    {
        foreach (var provider in Volatile.Read(ref _providers))
        {
            var source = provider.GetSource(key);
            if (source != null) return source;
        }
        return null;
    }

    private void Remove(IIconResolver provider)
    {
        lock (_gate)
            _providers = _providers.Where(p => !ReferenceEquals(p, provider)).ToArray();
    }

    private sealed class Registration(IconResolverRegistry owner, IIconResolver provider) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                owner.Remove(provider);
        }
    }
}

/// <summary>
/// Turns the icon key of a page (<see cref="IRouteableEntry.Path"/>) into what a platform displays.
/// A menu entry has no container to ask, so the resolvers of the application are kept here, once for the process, in
/// <see cref="Default"/>; a view that names its own <see cref="IRouteableEntry.Icons"/> uses those instead.
/// </summary>
public static class IconResolver
{
    /// <summary>The process-wide registry used by the views that do not name their own.</summary>
    public static IconResolverRegistry Default { get; } = new();

    /// <inheritdoc cref="IconResolverRegistry.Register"/>
    public static IDisposable Register(IIconResolver provider) => Default.Register(provider);

    public static object? GetIcon(string key) => Default.GetIcon(key);

    public static object? GetSource(string key) => Default.GetSource(key);
}
