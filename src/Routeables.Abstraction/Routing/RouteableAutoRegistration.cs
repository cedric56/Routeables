using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Reflection;

namespace Routeables.Abstraction;

/// <summary>
/// The routes the source generator found, handed to the host without the application calling <c>AddGeneratedRoutes()</c>:
/// the generator writes a module initializer in the executable that gives its registration method to
/// <see cref="Add(System.Action{Microsoft.Extensions.DependencyInjection.IServiceCollection})"/>, and each host
/// calls <see cref="Apply(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/> once the application has configured its services.
/// </summary>
/// <remarks>
/// What the application registered itself always wins. A route (a key) that is already registered, with <c>RegisterRoute</c>,
/// <c>AddRoutes</c> or a module, is left as it is, and so is any other service already there. When the application called
/// <c>AddGeneratedRoutes()</c> itself, nothing is applied: that call is the whole registration.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class RouteableAutoRegistration
{
    private static readonly object Gate = new();
    private static readonly List<(Assembly? Owner, Action<IServiceCollection> Run)> Registrations = [];

    /// <summary>Gives the registration of an executable. Called by the module initializer the generator writes.</summary>
    public static void Add(Action<IServiceCollection> registration) => Add(null, registration);

    /// <summary>
    /// Gives the registration of an executable, with the assembly that holds the generated code: only a call of the
    /// <c>AddGeneratedRoutes()</c> of that assembly (see <see cref="Mark(IServiceCollection, Assembly?)"/>) replaces it.
    /// </summary>
    public static void Add(Assembly? owner, Action<IServiceCollection> registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        lock (Gate)
            if (!Registrations.Exists(r => r.Run == registration))
                Registrations.Add((owner, registration));
    }

    /// <summary>Marks <paramref name="services"/> as holding every generated route: nothing is applied to it.</summary>
    public static void Mark(IServiceCollection services) => Mark(services, null);

    /// <summary>
    /// Marks <paramref name="services"/> as holding the routes of the <c>AddGeneratedRoutes()</c> of <paramref name="owner"/>, the
    /// assembly that holds the generated code. The <c>AddGeneratedRoutes()</c> of a library (called by a module of the application)
    /// does not hide the routes of the application's own.
    /// </summary>
    public static void Mark(IServiceCollection services, Assembly? owner)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!IsMarked(services, owner))
            services.AddSingleton(new GeneratedRoutesMarker(owner));
    }

    private static bool IsMarked(IServiceCollection services, Assembly? owner)
        => services.Any(d => d.ServiceType == typeof(GeneratedRoutesMarker) && d.ImplementationInstance is GeneratedRoutesMarker m
                             && (m.Owner is null || owner is null || m.Owner == owner));

    /// <summary>
    /// Adds the generated routes to <paramref name="services"/> unless the application did it with <c>AddGeneratedRoutes()</c>,
    /// leaving what is already registered alone.
    /// </summary>
    /// <returns><see langword="true"/> when routes were added.</returns>
    public static bool Apply(IServiceCollection services)
    {
        (Assembly? Owner, Action<IServiceCollection> Run)[] registrations;
        lock (Gate)
            registrations = [.. Registrations];
        return Apply(services, registrations);
    }

    internal static bool Apply(IServiceCollection services, IReadOnlyList<Action<IServiceCollection>> registrations)
        => Apply(services, [.. registrations.Select(r => ((Assembly?)null, r))]);

    internal static bool Apply(IServiceCollection services, IReadOnlyList<(Assembly? Owner, Action<IServiceCollection> Run)> registrations)
    {
        ArgumentNullException.ThrowIfNull(services);
        var pending = registrations.Where(r => !IsMarked(services, r.Owner)).ToList();
        if (pending.Count == 0)
            return false;

        var generated = new ServiceCollection();
        foreach (var (_, run) in pending)
            run(generated);

        Merge(generated, services);
        return true;
    }

    // Takes from the generated collection what the application did not register: a keyed service is the same one when its type and
    // its key are the same (a route, a region), and any other service when its type is.
    private static void Merge(IServiceCollection from, IServiceCollection into)
    {
        foreach (var descriptor in from)
        {
            if (descriptor.ImplementationInstance is RouteableTable table)
            {
                into.GetRouteableTable().AddRange(table);
                continue;
            }
            if (descriptor.ImplementationInstance is InjectionTable described)
            {
                into.GetInjectionTable().AddRange(described, overwrite: false);
                continue;
            }

            if (descriptor.ImplementationInstance is GeneratedRoutesMarker marker)
            {
                Mark(into, marker.Owner);
                continue;
            }

            var key = descriptor.IsKeyedService ? descriptor.ServiceKey : null;
            var present = descriptor.IsKeyedService
                ? into.Any(d => d.IsKeyedService && d.ServiceType == descriptor.ServiceType && Equals(d.ServiceKey, key))
                : into.Any(d => !d.IsKeyedService && d.ServiceType == descriptor.ServiceType);
            if (!present)
                into.Add(descriptor);
        }
    }
}

/// <summary>Present in a service collection that holds the routes of <c>AddGeneratedRoutes()</c>.</summary>
internal sealed class GeneratedRoutesMarker(Assembly? owner)
{
    /// <summary>The assembly whose generated routes are there; <see langword="null"/> for all of them.</summary>
    public Assembly? Owner { get; } = owner;
}
