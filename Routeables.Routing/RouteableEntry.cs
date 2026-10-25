using Routeables.Abstraction;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Routeables.Routing;

/// <summary>
/// Represents a navigable page in the application, pairing a view model type with the display
/// information needed to present it (name, icon/source, and launch behavior).
/// </summary>
public class RouteableEntry : IRouteableEntry, INotifyPropertyChanged
{
    public RouteableEntry(string route, string template, string displayName, string? path = null)
    {
        Template = template;
        Route = route;
        DisplayName = displayName;
        Path = path;
    }

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isLock;

    /// <summary>
    /// Gets or sets a value indicating whether the routeable reports it cannot be routed to.
    /// Updated by <see cref="ILockCoordinator.Subscribe"/>.
    /// </summary>
    public bool IsLock
    {
        get => _isLock;
        set
        {
            if (_isLock == value) return;
            _isLock = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(InverseIsLock));
        }
    }

    /// <summary>
    /// Gets the inverse of the <see cref="IsLock"/> property.
    /// </summary>
    internal bool InverseIsLock => !_isLock;

    /// <summary>The type of the view model this entry is for, when it is typed (<see cref="RouteableEntry{T}"/>); otherwise <see langword="null"/>.</summary>
    internal virtual Type? ViewModelType => null;

    /// <summary>
    /// Raises the <see cref="PropertyChanged"/> event for the specified property name.
    /// </summary>
    /// <param name="name">The name of the property that changed.</param>
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>
    /// Gets or sets the view model instance associated with this page, if one has been explicitly assigned.
    /// </summary>
    public object? ViewModel { get; set; }

    /// <summary>
    /// Gets or sets the view instance associated with this page.
    /// </summary>
    public object? View { get; set; }

    /// <summary>
    /// Gets or sets the region this page is shown in (the name given to <c>AddRegion</c> and to a <c>RouteableRegion</c>);
    /// <see langword="null"/> for the default router. <see cref="Routeables.Abstraction.Extensions.GoToAsync"/> follows it.
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Gets the display name for this page.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the path used to resolve this page's icon and source.
    /// </summary>
    public string? Path { get; }

    /// <summary>
    /// Gets or sets the resolvers used for this page's icon; <see langword="null"/> uses <see cref="IconResolver.Default"/>.
    /// </summary>
    public IIconResolver? Icons { get; set; }

    /// <summary>
    /// Gets or sets a short sentence shown under the display name by the route change menus.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets contextual data passed along when routing to this page.
    /// </summary>
    public IRouteableArgs Arguments { get; set; } = new RouteableArgs();

    public string Route { get; set; }

    public string Template { get; set; }

    /// <summary>
    /// Creates another entry for the same page: same route, template, name, icon, icons and description, without the
    /// instances (<see cref="ViewModel"/>, <see cref="View"/>), the <see cref="Region"/> or the <see cref="Arguments"/>.
    /// A typed entry (<see cref="RouteableEntry{T}"/>) copies as a typed entry, with no reflection.
    /// </summary>
    public virtual RouteableEntry Copy()
        => new(Route, Template, DisplayName, Path) { Description = Description, Icons = Icons };
}

/// <summary>
/// Strongly typed variant of <see cref="RouteableEntry"/> whose view model is known to be of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The view model type associated with this page.</typeparam>
public class RouteableEntry<T> : RouteableEntry
{
    internal override Type? ViewModelType => typeof(T);

    /// <summary>
    /// Initializes a new instance of the <see cref="RouteableEntry{T}"/> class with no pre-existing view model.
    /// </summary>
    /// <param name="displayName">The display name shown for this page.</param>
    /// <param name="template">The template.</param>
    /// <param name="icon">
    /// An optional path used to resolve the page's icon and source.
    /// </param>
    public RouteableEntry(string displayName, string? icon = null, string? route = null, string? template = null, string? description = null)
        : base(RouteableKeys.GetRoute(typeof(T), route), template ?? RouteableKeys.GetTemplate(typeof(T)), displayName, icon)
    {
        Description = description;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RouteableEntry{T}"/> class using an existing view model.
    /// </summary>
    /// <param name="viewModel">The existing view model instance to associate with this page.</param>
    /// <param name="displayName">The display name shown for this page.</param>
    /// <param name="template">The template.</param>
    /// <param name="icon">
    /// An optional path used to resolve the page's icon and source.
    /// </param>
    public RouteableEntry(T viewModel, string displayName, string? icon = null, string? route = null, string? template = null, string? description = null)
        : base(RouteableKeys.GetRoute(typeof(T), route), template ?? RouteableKeys.GetTemplate(typeof(T)), displayName, icon)
    {
        ViewModel = viewModel;
        Description = description;
    }

    /// <inheritdoc cref="RouteableEntry.Copy"/>
    public override RouteableEntry<T> Copy()
        => new(DisplayName, Path, Route, Template, Description) { Icons = Icons };
}