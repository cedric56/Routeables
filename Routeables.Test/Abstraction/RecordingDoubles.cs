using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Test.Abstraction;

/// <summary>An ordered log shared by several test doubles, to assert the order of cross-object calls.</summary>
public sealed class CallLog
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries => _entries;

    public void Add(string entry) => _entries.Add(entry);

    public void Clear() => _entries.Clear();
}

/// <summary>
/// A view model that records every lifecycle call as <c>"{name}:{Hook}"</c> in a shared <see cref="CallLog"/>
/// (hooks: CanLeave, Init, To, From, Destroy) and can be told to refuse or to fail.
/// </summary>
public class RecordingViewModel(string name, CallLog log) : IRouteable, IInitializeAsync
{
    public string Name { get; } = name;

    /// <summary>The answer given by <see cref="CanLeaveAsync"/>.</summary>
    public bool CanLeave { get; set; } = true;

    public Exception? InitializeFails { get; set; }
    public Exception? EnterFails { get; set; }
    public Exception? DestroyFails { get; set; }

    /// <summary>Runs inside <see cref="OnEnteringAsync"/> (for example to wait for a cancellation).</summary>
    public Func<CancellationToken, Task>? OnEnter { get; set; }

    /// <summary>Runs inside <see cref="DestroyAsync"/>, before the call is recorded as completed.</summary>
    public Action? OnDestroy { get; set; }

    public int InitializeCount { get; private set; }
    public int DestroyCount { get; private set; }
    public IRouteableArgs? InitializedWith { get; private set; }

    public Task<bool> CanLeaveAsync(CancellationToken ct = default)
    {
        log.Add($"{Name}:CanLeave");
        return Task.FromResult(CanLeave);
    }

    public Task InitializeAsync(IRouteableArgs args, CancellationToken ct = default)
    {
        log.Add($"{Name}:Init");
        InitializeCount++;
        InitializedWith = args;
        OnInitialized(args);
        return InitializeFails is null ? Task.CompletedTask : Task.FromException(InitializeFails);
    }

    public async Task OnEnteringAsync(IRouteableArgs context, CancellationToken ct = default)
    {
        log.Add($"{Name}:To");
        if (EnterFails is not null) throw EnterFails;
        if (OnEnter is not null) await OnEnter(ct);
    }

    public Task OnLeavingAsync(IRouteableArgs context, CancellationToken ct = default)
    {
        log.Add($"{Name}:From");
        return Task.CompletedTask;
    }

    public Task DestroyAsync(CancellationToken ct = default)
    {
        log.Add($"{Name}:Destroy");
        DestroyCount++;
        OnDestroy?.Invoke();
        return DestroyFails is null ? Task.CompletedTask : Task.FromException(DestroyFails);
    }

    protected virtual void OnInitialized(IRouteableArgs args)
    {
    }
}

/// <summary>A <see cref="RecordingViewModel"/> that is also a view; it remembers its <c>Context</c> when initialized.</summary>
public class RecordingView(string name, CallLog log) : RecordingViewModel(name, log), IRouteableFor
{
    public object? Context { get; set; }

    public object? ContextWhenInitialized { get; private set; }

    protected override void OnInitialized(IRouteableArgs args) => ContextWhenInitialized = Context;
}

/// <summary>Builds a real <see cref="Router"/> and routes through pairs of recording doubles.</summary>
internal static class RouterTestKit
{
    public static Router CreateRouter()
        => new(new RouterHistory(), new RouterGate(), new RouterLifecycle(), new ServiceCollection().BuildServiceProvider());

    /// <summary>Routes to a new page called <paramref name="name"/>: view model "vm{name}", view "view{name}".</summary>
    public static async Task<Page> GoToAsync(IRouter router, string name, CallLog log, IRouteableArgs? args = null)
    {
        var page = new Page(new RecordingViewModel($"vm{name}", log), new RecordingView($"view{name}", log));
        var result = await router.GoAsync(page.View, page.ViewModel, args, TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        return page;
    }

    /// <summary>Routes to the names in order and returns the pages.</summary>
    public static async Task<List<Page>> GoToAllAsync(IRouter router, CallLog log, params string[] names)
    {
        var pages = new List<Page>();
        foreach (var name in names)
            pages.Add(await GoToAsync(router, name, log));
        return pages;
    }

    public static IEnumerable<string> Destroyed(CallLog log)
        => log.Entries.Where(e => e.EndsWith(":Destroy"));
}

internal sealed record Page(RecordingViewModel ViewModel, RecordingView View);
