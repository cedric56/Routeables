namespace Routeables.Abstraction;

/// <summary>The kind of a route change.</summary>
public enum RouteableKind
{
    /// <summary>A new entry is added to the history.</summary>
    Push,
    /// <summary>The cursor moves one entry back.</summary>
    Back,
    /// <summary>The cursor moves one entry forward.</summary>
    Forward,
    /// <summary>The current entry leaves the history for good.</summary>
    Pop,
    /// <summary>The history goes back to its first entry.</summary>
    Root,
    /// <summary>The cursor moves to an entry by its index.</summary>
    MoveTo
}

/// <summary>How a route change ended.</summary>
public enum RouteableOutcome
{
    /// <summary>The route change was made.</summary>
    Success,
    /// <summary>The route change was refused (a guard, a page that cannot be left) or cancelled.</summary>
    Cancelled,
    /// <summary>The route change failed with an error.</summary>
    Failed
}
