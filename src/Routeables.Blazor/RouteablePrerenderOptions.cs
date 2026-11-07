namespace Routeables.Blazor;

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

    /// <summary>
    /// When <see langword="true"/> (the default), the history of the router goes from the prerendered page to the circuit (or to
    /// WebAssembly) that takes over with <see cref="Microsoft.AspNetCore.Components.PersistentComponentState"/>: the user finds the Back button
    /// where it was, and the pages keep what their view model saves (<see cref="Routeables.Abstraction.IRouteableState"/>).
    /// </summary>
    public bool PersistHistory { get; set; } = true;
}
