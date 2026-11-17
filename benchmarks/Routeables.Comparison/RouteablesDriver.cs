using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Guards;
using Routeables.Abstraction.Lifecycle;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;

namespace Routeables.Comparison;

public sealed class ListView : IRouteableFor { public object? Context { get; set; } }
public sealed class DetailView : IRouteableFor { public object? Context { get; set; } }
public sealed class EditView : IRouteableFor { public object? Context { get; set; } }
public sealed class SignInView : IRouteableFor { public object? Context { get; set; } }

public sealed class ListVm() : RVm("List");
public sealed class DetailVm(int id) : RVm($"Detail:{id}");

/// <summary>The edit page refuses to be left while it holds changes: the router asks it.</summary>
public sealed class EditVm(int id, AppState state) : RVm($"Edit:{id}"), IRouteable
{
    public Task<bool> CanLeaveAsync(CancellationToken ct = default) => Task.FromResult(!state.Dirty);
}

public sealed class SignInVm() : RVm("SignIn");

/// <summary>Pages that need a signed-in user are sent to the sign-in page; what was asked for is kept to go on after.</summary>
public sealed class SignedInGuard(AppState state) : IRouteGuard
{
    public Task<RouteGuardDecision> CanEnterAsync(RouteableRoutingEventArgs change, CancellationToken cancellationToken = default)
    {
        var route = change.To.Args.Route ?? "";
        var target = route.TrimStart('/').ToUpperInvariant();
        if (change.Kind != RouteableKind.Push || state.SignedIn || !AppState.IsGuarded(target))
            return Task.FromResult(RouteGuardDecision.Allow);
        state.Requested = target;
        return Task.FromResult(RouteGuardDecision.Redirect("/signin"));
    }
}

/// <summary>The scenario on the router of Routeables, the pages registered the way the generator registers a <c>[Routeable]</c> class.</summary>
public sealed class RouteablesDriver : INavigationDriver
{
    private readonly ServiceProvider _provider;
    private readonly IRouter _router;

    public RouteablesDriver()
    {
        State = new AppState();
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.AddSingleton(State);
        services.AddSingleton<IClock, Clock>();
        services.AddSingleton<IRouteGuard, SignedInGuard>();
        RouteablesPages.Register(services);
        const ServiceLifetime t = ServiceLifetime.Transient;
        services.RegisterRoute<ListView, ListVm>(t, t, "/list");
        services.RegisterRoute<DetailView, DetailVm>(t, t, "/detail/{id:int}");
        services.RegisterRoute<EditView, EditVm>(t, t, "/edit/{id:int}");
        services.RegisterRoute<SignInView, SignInVm>(t, t, "/signin");
        _provider = services.BuildServiceProvider();
        _router = _provider.GetRequiredService<IRouter>();
    }

    public string Name => "Routeables";

    public AppState State { get; }

    public bool SupportsForward => true;

    public string? Current => (_router.CurrentViewModel as RVm)?.Name;

    public object? CurrentObject => _router.CurrentViewModel;

    // A refused or turned-away change is part of the scenario, not an error: the page that is shown is what is checked.
    public Task GoAsync(string target) => _router.GoToAsync("/" + target.ToLowerInvariant());

    public Task BackAsync() => _router.GoBackAsync();

    public Task ForwardAsync() => _router.ForwardAsync();

    public async Task<bool> ClearHistoryAsync()
    {
        await _router.EraseHistoryAsync();
        return true;
    }

    public void Dispose() => _provider.Dispose();
}
