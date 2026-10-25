using Microsoft.Extensions.DependencyInjection;

namespace Routeables.Abstraction;

/// <summary>
/// Specifies that a parameter should be injected via property or constructor dependency injection.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class InjectPropertyAttribute : Attribute
{
}

/// <summary>
/// Marks a class as routeable within the framework, defining its route change path, region, and service lifetime.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="RouteableAttribute"/> class.
/// </remarks>
/// <param name="path">The URI or routing path associated with the routeable target.</param>
/// <param name="region">Optional region identifier where the routeable target resides.</param>
/// <param name="serviceLifetime">The dependency injection service lifetime, defaulting to transient.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RouteableAttribute(string path, string? region = null,
    ServiceLifetime serviceLifetime = ServiceLifetime.Transient) : Attribute
{
    /// <summary>
    /// Gets the route change path associated with this routeable target.
    /// </summary>
    public string? Path { get; } = path;

    /// <summary>
    /// Gets or sets the region identifier where this routeable target resides.
    /// </summary>
    public string? Region { get; set; } = region;

    /// <summary>
    /// Gets or sets the dependency injection service lifetime for the routeable target.
    /// </summary>
    public ServiceLifetime Lifetime { get; set; } = serviceLifetime;
}

/// <summary>
/// Associates a view with a specific view model, route, region, and service lifetime.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="RouteableViewAttribute"/> class.
/// </remarks>
/// <param name="viewModelType">The type of the view model associated with the view.</param>
/// <param name="route">Optional route string for routing.</param>
/// <param name="region">Optional region identifier.</param>
/// <param name="serviceLifetime">The dependency injection service lifetime of the view and of its view model, defaulting to transient.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RouteableViewAttribute(Type viewModelType, string? route = null, string? region = null, ServiceLifetime serviceLifetime = ServiceLifetime.Transient) : Attribute
{
    /// <summary>
    /// Gets the region identifier where this routeable view is displayed.
    /// </summary>
    public string? Region { get; } = region;

    /// <summary>
    /// Gets the route change route associated with this view.
    /// </summary>
    public string? Route { get; } = route;

    /// <summary>
    /// Gets the type of the view model paired with this view.
    /// </summary>
    internal Type ViewModelType { get; } = viewModelType;

    /// <summary>
    /// Gets or sets the lifetime of the view and of its view model, unless <see cref="ViewLifetime"/> or
    /// <see cref="ViewModelLifetime"/> says otherwise.
    /// </summary>
    public ServiceLifetime Lifetime { get; set; } = serviceLifetime;

    private ServiceLifetime? _viewModelLifetime;
    private ServiceLifetime? _viewLifetime;

    /// <summary>
    /// Gets or sets the lifetime of the view model alone; <see cref="Lifetime"/> when not set.
    /// <c>[RouteableView(typeof(CartViewModel), ViewModelLifetime = ServiceLifetime.Singleton)]</c> keeps one cart
    /// while the view is built again for each route change.
    /// </summary>
    public ServiceLifetime ViewModelLifetime
    {
        get => _viewModelLifetime ?? Lifetime;
        set => _viewModelLifetime = value;
    }

    /// <summary>
    /// Gets or sets the lifetime of the view alone; <see cref="Lifetime"/> when not set.
    /// </summary>
    public ServiceLifetime ViewLifetime
    {
        get => _viewLifetime ?? Lifetime;
        set => _viewLifetime = value;
    }
}

/// <summary>
/// Marks a view model as a route change destination and declares which types may go to it.
/// For each caller type, the Routeables source generator emits an extension method on that caller
/// (<c>GoTo{Name}Async</c>) whose parameters mirror the destination's constructor, so the arguments
/// are checked at compile time and the method is visible only on the declared callers.
/// </summary>
/// <remarks>
/// <para>
/// The route template comes from the <see cref="RouteableAttribute"/> on the same class, or from
/// <see cref="Route"/>. Every constructor parameter becomes a parameter of the generated method, except
/// parameters marked with <see cref="FromContainerAttribute"/>, <c>[FromServices]</c> or <c>[FromKeyedServices]</c>, which are
/// resolved from the container. A parameter whose name matches a <c>{placeholder}</c> of the template is
/// written into the route instead of being passed as an argument. A constructor parameter of type
/// <see cref="IRouter"/> receives the router the method is called with.
/// </para>
/// <para>
/// The constructor is chosen like <c>ActivatorUtilities</c> does: the one marked
/// <c>[ActivatorUtilitiesConstructor]</c>, otherwise the public one with the most parameters.
/// </para>
/// </remarks>
/// <param name="callerTypes">The types that get a route change method to this view model.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RouteableViewModelAttribute(params Type[] callerTypes) : Attribute
{
    /// <summary>Gets the types that get a route change method to this view model.</summary>
    internal Type[] CallerTypes { get; } = callerTypes;

    /// <summary>
    /// Gets or sets the route routed to; placeholders are filled from the constructor arguments. Defaults to the
    /// path of the single <see cref="RouteableAttribute"/> on the class; required when there are several.
    /// </summary>
    public string? Route { get; set; }

    /// <summary>
    /// Gets or sets the route template handed to the router to match <see cref="Route"/> against. Defaults to
    /// <see cref="Route"/>, which in turn defaults to the path of the single <see cref="RouteableAttribute"/>.
    /// </summary>
    public string? Template { get; set; }

    /// <summary>
    /// Gets or sets the name of the generated method. Defaults to <c>GoTo{Name}Async</c>, where
    /// <c>{Name}</c> is the class name without its <c>ViewModel</c> suffix.
    /// </summary>
    public string? MethodName { get; set; }
}

/// <summary>
/// Marks a constructor parameter of a <see cref="RouteableViewModelAttribute"/> view model as resolved
/// from the dependency injection container: the generated route change method does not ask the caller for it.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class FromContainerAttribute : Attribute
{
}

/// <summary>
/// Marks a property of a view or a view model that receives a route change value: a template parameter written
/// <c>{name::}</c> (see "Parameters by attribute" in the guides). The same attribute is used on every host that has no
/// native one; on MAUI it is read like <c>QueryPropertyAttribute</c>, which remains the native way (on the class, naming the property).
/// </summary>
public class RouteableParameterAttribute : Attribute
{
}
