namespace Routeables.Abstraction;

/// <summary>
/// What <c>GoToForResultAsync</c> completes with, once the page it opened is left: whether the page opened, and the value it gave
/// back, if it gave one.
/// </summary>
/// <typeparam name="TResult">The type of the value the page gives back.</typeparam>
public sealed class RouteableResult<TResult>
{
    /// <summary>The result of opening the page. When it failed or was cancelled, nothing was opened and <see cref="Returned"/> is <see langword="false"/>.</summary>
    public RouteableResult OpenResult { get; init; } = RouteableResult.Ok();

    /// <summary>Whether the page gave a value back. <see langword="false"/> when it was left without choosing, or never opened.</summary>
    public bool Returned { get; init; }

    /// <summary>The value the page gave back; <see langword="default"/> when <see cref="Returned"/> is <see langword="false"/>.</summary>
    public TResult? Value { get; init; }

    /// <summary>Whether the wait was cancelled by its token before the page was left.</summary>
    public bool Cancelled { get; init; }

    /// <summary>The value, or <paramref name="fallback"/> when the page gave none.</summary>
    public TResult? ValueOr(TResult? fallback) => Returned ? Value : fallback;
}
