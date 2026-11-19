using Microsoft.Extensions.DependencyInjection;

namespace Routeables.Comparison;

/// <summary>
/// The navigation service most applications start with: a dictionary from a route to a factory, a list of pages and an index, a guard and
/// a "can I leave" test written by hand. About what it takes to do without a library; nothing here is clever.
/// </summary>
public sealed class HomeGrownDriver : INavigationDriver
{
    public sealed class Page(string name)
    {
        public string Name { get; } = name;
        public Func<bool> CanLeave { get; init; } = static () => true;
        public IClock? Clock { get; init; }
    }

    private readonly ServiceProvider _provider;
    private readonly Dictionary<string, Func<string[], Page>> _routes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Page> _pages = [];
    private int _index = -1;

    public HomeGrownDriver()
    {
        State = new AppState();
        var services = new ServiceCollection();
        services.AddSingleton(State);
        services.AddSingleton<IClock, Clock>();
        _provider = services.BuildServiceProvider();

        for (var n = 0; n < 40; n++)
        {
            var name = $"P{n:00}";
            if (n is >= 30 and <= 34)
                _routes[name] = _ => { var clock = _provider.GetRequiredService<IClock>(); return new Page(name) { Clock = clock }; }; // a page with a dependency
            else
                _routes[name] = _ => new Page(name);
        }

        for (var n = 0; n < 10; n++)
        {
            var c = $"C{n}";
            _routes[c] = args => new Page($"{c}:{args[0]}");
        }

        _routes["List"] = _ => new Page("List");
        _routes["Detail"] = args => new Page($"Detail:{args[0]}");
        _routes["Edit"] = args => new Page($"Edit:{args[0]}") { CanLeave = () => !State.Dirty };
        _routes["SignIn"] = _ => new Page("SignIn");
    }

    public string Name => "Home-grown service";

    public AppState State { get; }

    public bool SupportsForward => true;

    public string? Current => _index < 0 ? null : _pages[_index].Name;

    public object? CurrentObject => _index < 0 ? null : _pages[_index];

    public Task GoAsync(string target)
    {
        if (!CanLeave())
            return Task.CompletedTask;
        if (AppState.IsGuarded(target) && !State.SignedIn)
        {
            State.Requested = target;
            target = "SignIn";
        }

        var parts = target.Split('/');
        var page = _routes[parts[0]](parts[1..]);
        _pages.RemoveRange(_index + 1, _pages.Count - _index - 1);
        _pages.Add(page);
        _index++;
        return Task.CompletedTask;
    }

    public Task BackAsync()
    {
        if (_index > 0 && CanLeave())
            _index--;
        return Task.CompletedTask;
    }

    public Task ForwardAsync()
    {
        if (_index < _pages.Count - 1 && CanLeave())
            _index++;
        return Task.CompletedTask;
    }

    public Task<bool> ClearHistoryAsync()
    {
        var current = _pages[_index];
        _pages.Clear();
        _pages.Add(current);
        _index = 0;
        return Task.FromResult(true);
    }

    private bool CanLeave() => _index < 0 || _pages[_index].CanLeave();

    public void Dispose() => _provider.Dispose();
}
