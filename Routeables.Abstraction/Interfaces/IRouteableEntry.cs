namespace Routeables.Abstraction;

/// <summary>
/// An entry of the menu, and a destination of the router: a name, a route, the view and view model it shows, and an icon. The main
/// view model (<see cref="IMainViewModel"/>) lists them; <c>RouteableEntry&lt;TViewModel&gt;</c> is the usual implementation.
/// </summary>
public interface IRouteableEntry
{
    /// <summary>Whether the menu refuses to select this entry (see <see cref="IMainViewModel.Lock"/>).</summary>
    bool IsLock { get; set; }

    /// <summary>The view model instance of the entry, when one has been assigned explicitly; <see langword="null"/> lets the router build it.</summary>
    object? ViewModel { get; set; }

    /// <summary>The view instance of the entry, when one has been assigned or created; <see langword="null"/> before.</summary>
    object? View { get; set; }

    //Type? ViewModelType { get; }

    /// <summary>The address of the entry (<c>/cats</c>), as it is given to the router.</summary>
    string Route { get; }

    /// <summary>The template of the route (<c>/cats/{name}</c>), which names the parameters the address carries.</summary>
    string Template { get; }

    /// <summary>The arguments the route change to this entry is made with.</summary>
    IRouteableArgs Arguments { get; set; }

    /// <summary>The name the menu shows for this entry.</summary>
    string DisplayName { get; }

    /// <summary>
    /// The region this entry is shown in: the name given to <c>AddRegion</c>, which is also the name a <c>RouteableRegion</c>
    /// of a view is given. <see langword="null"/> is the default router (the main area). See
    /// <see cref="Extensions.RouterFor(IServiceProvider, IRouteableEntry)"/>.
    /// </summary>
    string? Region => null;

    /// <summary>A short sentence shown under the name (optional).</summary>
    string? Description => null;

    /// <summary>Whether <see cref="Description"/> has text (bindable, for the menus).</summary>
    bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    /// <summary>The key of the icon of this entry, given to the resolvers (see <see cref="IIconResolver"/>); <see langword="null"/> for none.</summary>
    string? Path { get; }

    /// <summary>
    /// The resolvers that turn <see cref="Path"/> into <see cref="Icon"/> and <see cref="Source"/>; <see langword="null"/>
    /// uses the process-wide <see cref="IconResolver.Default"/>.
    /// </summary>
    IIconResolver? Icons => null;

    /// <summary>The icon of this entry as a control, from <see cref="Path"/>; <see langword="null"/> when there is no key or no resolver knows it.</summary>
    object? Icon
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Path)) return null;
            return (Icons ?? IconResolver.Default).GetIcon(Path);
        }
    }

    /// <summary>The icon of this entry as an image source, from <see cref="Path"/>; <see langword="null"/> when there is no key or no resolver knows it.</summary>
    object? Source
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Path)) return null;
            return (Icons ?? IconResolver.Default).GetSource(Path);
        }
    }
}
