namespace Routeables.Regions;

/// <summary>The errors that every region host reports the same way.</summary>
public static class RegionErrors
{
    /// <summary>
    /// A view has one parent. Says who holds it, instead of the bare "already the child of another element" of the framework.
    /// </summary>
    /// <param name="view">The view the region was asked to show.</param>
    /// <param name="holder">The element that already displays it.</param>
    /// <param name="region">The name of the region; empty for the default one.</param>
    public static InvalidOperationException AlreadyDisplayed(object view, object holder, string? region)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(holder);
        return new InvalidOperationException(
            $"{view.GetType().Name} is already displayed by {holder.GetType().Name}, so region '{region ?? "(default)"}' cannot show it too. " +
            "A page shown by a region must not also host its own current view: let the region display it.");
    }
}
