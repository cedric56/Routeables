namespace Routeables.Abstraction.History;

/// <summary>Read-only view of one history entry.</summary>
public interface IHistoryEntry
{
    /// <summary>The view model of this entry.</summary>
    object ViewModel { get; }

    /// <summary>The view of this entry.</summary>
    object View { get; }

    /// <summary>The arguments the route change to this entry was made with.</summary>
    IRouteableArgs Args { get; }
}
