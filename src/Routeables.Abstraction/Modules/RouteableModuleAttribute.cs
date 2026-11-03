namespace Routeables.Abstraction;

/// <summary>
/// Marks the method that adds a module to the application: a <c>public static</c> method with one <see cref="Microsoft.Extensions.DependencyInjection.IServiceCollection"/>
/// parameter, usually the <c>AddXxxModule()</c> extension method of the module. <see cref="IRouteableModules.LoadFrom"/> and
/// <see cref="IRouteableModules.Load(System.Reflection.Assembly)"/> find it by this attribute and call it on the services of the module.
/// <code>
/// [RouteableModule("Orders")]
/// public static IServiceCollection AddOrdersModule(this IServiceCollection services)
///     => services.AddGeneratedRoutes().AddMenuEntries(() => [new RouteableEntry&lt;OrdersViewModel&gt;("Orders", route: "/orders", template: "/orders")]);
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RouteableModuleAttribute(string? name = null) : Attribute
{
    /// <summary>The name of the module; the name of the method when it is not given.</summary>
    public string? Name { get; } = name;
}
