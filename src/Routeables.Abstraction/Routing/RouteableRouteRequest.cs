namespace Routeables.Abstraction;

/// <summary>
/// What <see cref="IRouter.GoToRouteAsync"/> is asked to do: the one member behind the <c>GoToRouteAsync</c> methods you call
/// (<see cref="RouterExtensions"/>). You rarely build it yourself; call those methods.
/// </summary>
public sealed class RouteableRouteRequest
{
    /// <summary>The route to go to, with its values (<c>"/orders/42"</c>). Not used when <see cref="Entry"/> is set (the entry knows its own).</summary>
    public string? Route { get; init; }

    /// <summary>The registered template, when the route alone does not tell it. Found from the route when <see langword="null"/>.</summary>
    public string? Template { get; init; }

    /// <summary>The type of the view model to build. <see langword="null"/> for an entry that names its own.</summary>
    public Type? ViewModelType { get; init; }

    /// <summary>The arguments of the route change, or <see langword="null"/> for none.</summary>
    public IRouteableArgs? Args { get; init; }

    /// <summary>The menu entry to go to. It remembers the page it showed last.</summary>
    public IRouteableEntry? Entry { get; init; }

    /// <summary>Names of the route values that go to the view (a property marked <c>[Parameter]</c>, <c>[QueryProperty]</c> or <c>[RouteableParameter]</c>) instead of the view model.</summary>
    public string[] ViewKeys { get; init; } = [];
}
