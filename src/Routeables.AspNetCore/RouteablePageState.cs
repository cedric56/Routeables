namespace Routeables.AspNetCore;

/// <summary>What the page remembers of the visitor between two requests (it lives in the scope of the session).</summary>
public sealed class RouteablePageState
{
    /// <summary>
    /// The address the router was last taken to. A request for the same address shows the state as it is (a reload does not
    /// start the page again); another address navigates.
    /// </summary>
    public string? ServedUrl { get; set; }
}
