using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction.Routing;
using System.ComponentModel;

namespace Routeables.Abstraction.Registrations;

/// <summary>
/// The arguments of a constructor the source generator calls directly: what <see cref="ActivatorUtilities"/> does by reflection,
/// without it. A parameter takes, in order, a value the route change gave (by name first, then the first one of its type that no other parameter claims by its name), the router
/// that routes, or else the service the container has for its type, or its default. It is meant for the generated code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ConstructorArguments
{
    private readonly IServiceProvider provider;
    private readonly string typeName;
    // Each value with the name it was given under (null: none), and the names of the parameters of the constructor: a value that has the
    // name of a parameter is for that parameter only, the others go to the first parameter of their type.
    private readonly List<(string? Name, object Value)> values;
    private readonly HashSet<string> parameterNames;
    private readonly bool[] used;

    private ConstructorArguments(IServiceProvider provider, string typeName, List<(string? Name, object Value)> values, IEnumerable<string> parameterNames)
    {
        this.provider = provider;
        this.typeName = typeName;
        this.values = values;
        this.parameterNames = new HashSet<string>(parameterNames, StringComparer.OrdinalIgnoreCase);
        used = new bool[values.Count];
    }

    /// <summary>
    /// The arguments of a view model: its view-model values in the order of the parameter names, and the router that routes
    /// when the view model takes one and was not given one.
    /// </summary>
    public static ConstructorArguments ForViewModel(
        IServiceProvider provider, IRouteableArgs args, string typeName, string[] parameterNames, bool takesRouter)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(parameterNames);

        var values = args.ViewModelParameters
            .Where(p => p.attribute == false && p.value is not null)
            .Select(p => (Name: string.IsNullOrEmpty(p.name) ? null : p.name, Value: p.value!))
            .ToList();

        if (takesRouter && args.Router is { } router && !values.Any(v => v.Value is IRouter))
            values.Add((null, router));

        return new ConstructorArguments(provider, typeName, values, parameterNames);
    }

    /// <summary>The arguments of a view: its view values, in the order they were given.</summary>
    public static ConstructorArguments ForView(IServiceProvider provider, IRouteableArgs args, string typeName)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(args);

        var values = args.ViewParameters.Where(p => p.attribute == false && p.value is not null).Select(p => ((string?)null, p.value!)).ToList();
        return new ConstructorArguments(provider, typeName, values, []);
    }

    /// <summary>The value of a parameter that has no default: a value given, else the service of its type.</summary>
    public T Next<T>(string parameterName)
    {
        if (TakeGiven<T>(parameterName, out var given))
            return given;

        return provider.GetService(typeof(T)) is T service
            ? service
            : throw new InvalidOperationException(
                $"Unable to resolve service for type '{typeof(T)}' while attempting to activate '{typeName}' (parameter '{parameterName}').");
    }

    /// <summary>The value of a parameter that has a default: a value given, else the service of its type, else the default.</summary>
    public T Next<T>(string parameterName, T defaultValue)
    {
        if (TakeGiven<T>(parameterName, out var given))
            return given;

        return provider.GetService(typeof(T)) is T service ? service : defaultValue;
    }

    private bool TakeGiven<T>(string parameterName, out T value)
    {
        // The value given under the name of the parameter first: "/products/{id}" gives id, never category, whatever the types.
        for (var i = 0; i < values.Count; i++)
        {
            if (!used[i] && values[i].Name is { } name && string.Equals(name, parameterName, StringComparison.OrdinalIgnoreCase) && values[i].Value is T named)
            {
                used[i] = true;
                value = named;
                return true;
            }
        }

        // Then the first of the type that is for no parameter by its name: a value without a name, or one that no parameter has the name of.
        for (var i = 0; i < values.Count; i++)
        {
            if (!used[i] && (values[i].Name is not { } name || !parameterNames.Contains(name)) && values[i].Value is T match)
            {
                used[i] = true;
                value = match;
                return true;
            }
        }

        value = default!;
        return false;
    }
}
