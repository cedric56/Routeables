namespace Routeables.Razor;

/// <summary>
/// What the layout does while the server prerenders a page (the first response, before the circuit exists). <b>Registering it switches
/// prerendering on</b>: without it the layout waits for the circuit, as it always did. Register one with
/// <c>services.AddSingleton(new RouteablePrerenderOptions { ... })</c> in the block of <c>UseRouteablesServer</c>.
/// </summary>
public sealed class RouteablePrerenderOptions
{
    /// <summary>
    /// When <see langword="true"/>, an address that matches no route (other than <c>/</c>) answers with the status 404 and the not-found
    /// page of the application's router instead of the default page. Without it (the default) such an address shows the default page,
    /// as it does once the circuit runs. Prerendering only: the interactive behaviour does not change.
    /// </summary>
    public bool UnknownAddressIsNotFound { get; set; }
}
