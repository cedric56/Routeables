using Routeables.Abstraction;

namespace Routeables.Tests.Abstraction;

/// <summary>
/// <see cref="RouteableIconResolver"/> is process-wide: every test uses its own GUID key, so none depends on another.
/// </summary>
public class IconResolverRegistrationTests
{
    private sealed class FixedResolver(string key, object icon) : IRouteableIconResolver
    {
        public int Calls { get; private set; }

        public object? GetIcon(string requested)
        {
            Calls++;
            return requested == key ? icon : null;
        }

        public object? GetSource(string requested) => null;
    }

    [Fact]
    public void Disposing_the_token_removes_the_provider()
    {
        var key = $"token-{Guid.NewGuid()}";
        var icon = new object();
        var token = RouteableIconResolver.Register(new FixedResolver(key, icon));
        Assert.Same(icon, RouteableIconResolver.GetIcon(key));

        token.Dispose();
        token.Dispose();

        Assert.Null(RouteableIconResolver.GetIcon(key));
    }

    [Fact]
    public void The_same_provider_registered_twice_is_kept_once()
    {
        var key = $"twice-{Guid.NewGuid()}";
        var provider = new FixedResolver(key, new object());

        var first = RouteableIconResolver.Register(provider);
        var second = RouteableIconResolver.Register(provider);
        RouteableIconResolver.GetIcon(key);

        Assert.Equal(1, provider.Calls);

        first.Dispose();
        Assert.Null(RouteableIconResolver.GetIcon(key));
        second.Dispose();
    }

    [Fact]
    public async Task Registering_from_several_threads_loses_no_provider()
    {
        var keys = Enumerable.Range(0, 100).Select(_ => $"parallel-{Guid.NewGuid()}").ToList();
        var icons = keys.ToDictionary(k => k, _ => new object());

        await Task.WhenAll(keys.Select(k => Task.Run(() => RouteableIconResolver.Register(new FixedResolver(k, icons[k])))));

        foreach (var key in keys)
            Assert.Same(icons[key], RouteableIconResolver.GetIcon(key));
    }
}
