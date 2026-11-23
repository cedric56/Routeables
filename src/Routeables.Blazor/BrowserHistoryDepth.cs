using Routeables.Abstraction.Routing;

namespace Routeables.Blazor;

/// <summary>How many entries the browser holds behind and ahead of the current one that the application pushed (or reached by a Back).</summary>
internal sealed class BrowserHistoryDepth
{
    /// <summary>The entries behind the current one that this application pushed.</summary>
    public int Back { get; private set; }

    /// <summary>The entries ahead of the current one, left by a Back.</summary>
    public int Forward { get; private set; }

    /// <summary>The router history no longer follows the browser's: counts again from nothing.</summary>
    public void Reset() => Back = Forward = 0;

    /// <summary>Whether the browser holds the entry a router move of this kind goes to (a Pop goes back like a Back).</summary>
    public bool CanFollow(RouteableKind kind)
        => kind is RouteableKind.Back or RouteableKind.Pop && Back > 0 || kind == RouteableKind.Forward && Forward > 0;

    /// <summary>Counts a move of the router that the browser makes too.</summary>
    public void Track(RouteableKind kind)
    {
        switch (kind)
        {
            case RouteableKind.Push:
                Back++;
                Forward = 0;
                break;
            case RouteableKind.Back:
                if (Back > 0) { Back--; Forward++; }
                break;
            case RouteableKind.Forward:
                if (Forward > 0) { Forward--; Back++; }
                break;
            case RouteableKind.Pop:
                // The router forgets the page it leaves, while the browser keeps it ahead and reaches it again as a new page if asked.
                if (Back > 0) Back--;
                Forward = 0;
                break;
            default:
                Reset();
                break;
        }
    }
}
