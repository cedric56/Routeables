using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace Routeables.Abstraction;

/// <summary>
/// The routes the source generator found, handed to the host without the application calling <c>AddGeneratedRoutes()</c>: the
/// generator writes a module initializer in the executable that gives its registration method to <see cref="Add"/>, and each host
/// calls <see cref="Apply"/> once the application has configured its services. It is meant for the generated code and the hosts.
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
    private static readonly List<Action<IServiceCollection>> Registrations = [];

    /// <summary>Gives the registration of an executable. Called by the module initializer the generator writes.</summary>
    public static void Add(Action<IServiceCollection> registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        lock (Gate)
            if (!Registrations.Contains(registration))
                Registrations.Add(registration);
    }

    /// <summary>Marks <paramref name="services"/> as holding the generated routes: the generated <c>AddGeneratedRoutes()</c> calls it.</summary>
    public static void Mark(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!services.Any(d => d.ServiceType == typeof(GeneratedRoutesMarker)))
            services.AddSingleton(new GeneratedRoutesMarker());
    }

    /// <summary>
    /// Adds the generated routes to <paramref name="services"/> unless the application did it with <c>AddGeneratedRoutes()</c>,
    /// leaving what is already registered alone.
    /// </summary>
    /// <returns><see langword="true"/> when routes were added.</returns>
    public static bool Apply(IServiceCollection services)
    {
        Action<IServiceCollection>[] registrations;
        lock (Gate)
            registrations = [.. Registrations];
        return Apply(services, registrations);
    }

    internal static bool Apply(IServiceCollection services, IReadOnlyList<Action<IServiceCollection>> registrations)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (registrations.Count == 0 || services.Any(d => d.ServiceType == typeof(GeneratedRoutesMarker)))
            return false;

        var generated = new ServiceCollection();
        foreach (var registration in registrations)
            registration(generated);

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
internal sealed class GeneratedRoutesMarker;
