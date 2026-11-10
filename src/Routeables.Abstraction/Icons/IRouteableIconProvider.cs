namespace Routeables.Abstraction.Icons;

/// <summary>
/// Gives a page its icon from a key (<see cref="IRouteableMenuEntry.Path"/>). Implement it to draw icons from your own set, a font or
/// resources, and register it with <see cref="RouteableIconResolver.Register"/>; the first resolver that knows a key wins.
/// </summary>
public interface IRouteableIconResolver
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
/// ones (tests, several hosts) creates its own and gives it to its views with <see cref="IRouteableMenuEntry.Icons"/>.
/// </summary>
public sealed class RouteableIconResolverRegistry : IRouteableIconResolver
{
    private readonly object _gate = new();
    private IRouteableIconResolver[] _providers = [];

    /// <summary>Adds <paramref name="provider"/> after the ones already registered; the same instance is kept once.</summary>
    /// <returns>A token: disposing it removes the provider again.</returns>
    public IDisposable Register(IRouteableIconResolver provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        lock (_gate)
        {
            if (!_providers.Any(p => ReferenceEquals(p, provider)))
                _providers = [.. _providers, provider];
        }

        return new Registration(this, provider);
    }

    /// <inheritdoc />
    public object? GetIcon(string key)
    {
        foreach (var provider in Volatile.Read(ref _providers))
        {
            var icon = provider.GetIcon(key);
            if (icon != null) return icon;
        }
        return null;
    }

    /// <inheritdoc />
    public object? GetSource(string key)
    {
        foreach (var provider in Volatile.Read(ref _providers))
        {
            var source = provider.GetSource(key);
            if (source != null) return source;
        }
        return null;
    }

    private void Remove(IRouteableIconResolver provider)
    {
        lock (_gate)
            _providers = _providers.Where(p => !ReferenceEquals(p, provider)).ToArray();
    }

    private sealed class Registration(RouteableIconResolverRegistry owner, IRouteableIconResolver provider) : IDisposable
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
/// Turns the icon key of a page (<see cref="IRouteableMenuEntry.Path"/>) into what a platform displays.
/// A menu entry has no container to ask, so the resolvers of the application are kept here, once for the process, in
/// <see cref="Default"/>; a view that names its own <see cref="IRouteableMenuEntry.Icons"/> uses those instead.
/// </summary>
public static class RouteableIconResolver
{
    /// <summary>The process-wide registry used by the views that do not name their own.</summary>
    public static RouteableIconResolverRegistry Default { get; } = new();

    /// <inheritdoc cref="RouteableIconResolverRegistry.Register"/>
    public static IDisposable Register(IRouteableIconResolver provider) => Default.Register(provider);

    /// <inheritdoc cref="RouteableIconResolverRegistry.GetIcon"/>
    public static object? GetIcon(string key) => Default.GetIcon(key);

    /// <inheritdoc cref="RouteableIconResolverRegistry.GetSource"/>
    public static object? GetSource(string key) => Default.GetSource(key);
}
