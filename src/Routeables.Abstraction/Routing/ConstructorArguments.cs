using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace Routeables.Abstraction;

/// <summary>
/// The arguments of a constructor the source generator calls directly: what <see cref="ActivatorUtilities"/> does by reflection,
/// without it. A parameter takes, in order, a value the route change gave (by name first, then the first one of its type), the router
/// that routes, or else the service the container has for its type, or its default. It is meant for the generated code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ConstructorArguments
{
    private readonly IServiceProvider provider;
    private readonly string typeName;
    private readonly List<object> values;
    private readonly bool[] used;

    private ConstructorArguments(IServiceProvider provider, string typeName, List<object> values)
    {
        this.provider = provider;
        this.typeName = typeName;
        this.values = values;
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

        var positions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < parameterNames.Length; i++)
            positions[parameterNames[i]] = i;

        var values = Extensions.BindByName(
            args.ViewModelParameters.Where(p => p.attribute == false && p.value is not null), [positions]);

        if (takesRouter && args.Router is { } router && !values.Any(v => v is IRouter))
            values.Add(router);

        return new ConstructorArguments(provider, typeName, values);
    }

    /// <summary>The arguments of a view: its view values, in the order they were given.</summary>
    public static ConstructorArguments ForView(IServiceProvider provider, IRouteableArgs args, string typeName)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(args);

        var values = args.ViewParameters.Where(p => p.attribute == false && p.value is not null).Select(p => p.value!).ToList();
        return new ConstructorArguments(provider, typeName, values);
    }

    /// <summary>The value of a parameter that has no default: a value given, else the service of its type.</summary>
    public T Next<T>(string parameterName)
    {
        if (TakeGiven<T>(out var given))
            return given;

        return provider.GetService(typeof(T)) is T service
            ? service
            : throw new InvalidOperationException(
                $"Unable to resolve service for type '{typeof(T)}' while attempting to activate '{typeName}' (parameter '{parameterName}').");
    }

    /// <summary>The value of a parameter that has a default: a value given, else the service of its type, else the default.</summary>
    public T Next<T>(string parameterName, T defaultValue)
    {
        if (TakeGiven<T>(out var given))
            return given;

        return provider.GetService(typeof(T)) is T service ? service : defaultValue;
    }

    private bool TakeGiven<T>(out T value)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (!used[i] && values[i] is T match)
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
