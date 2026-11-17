using System.Runtime.CompilerServices;
using Routeables.Abstraction.Routing;

namespace Routeables.Services;

/// <summary>
/// The order of what happens to a router when the application starts: the history of the last run is put back first, then the addresses
/// that opened the application are followed. Both wait for the first page; without this, the one that is quicker decides, and a
/// restoration (which starts by erasing the history) can remove the page that a link has just opened.
/// </summary>
internal static class RouteableStartup
{
    private static readonly ConditionalWeakTable<IRouter, TaskCompletionSource> Restorations = new();

    /// <summary>The history of this router is going to be put back: the addresses wait for it.</summary>
    internal static void BeginRestore(IRouter router)
        => Restorations.GetValue(router, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

    /// <summary>The history is back, or cannot be: the addresses may be followed.</summary>
    internal static void EndRestore(IRouter router)
    {
        if (Restorations.TryGetValue(router, out var restoration))
            restoration.TrySetResult();
    }

    /// <summary>Completes once the history of this router is back; at once when none is being put back.</summary>
    internal static Task WhenRestoredAsync(IRouter router)
        => Restorations.TryGetValue(router, out var restoration) ? restoration.Task : Task.CompletedTask;
}
