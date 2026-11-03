using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Routeables.Abstraction;

/// <summary>Injection of the view and view model properties and constructors from the route arguments.</summary>
public static partial class Extensions
{
    /// <summary>
    /// Populates the injectable properties of <paramref name="view"/> (view or view model):
    /// <list type="number">
    /// <item>A property of type <see cref="RouteablesParameters"/> receives the whole parameter bag.</item>
    /// <item>A property whose name matches an entry in <paramref name="parameters"/> (case-insensitive)
    /// receives that value. Explicit values always win, and <c>null</c> is a valid value.</item>
    /// <item>Any other eligible property that is still <c>null</c> is resolved from DI by its type, when one of its
    /// attributes is registered with <see cref="RouteablePropertyEntry.FromContainer"/> (<c>[Inject]</c>); a value
    /// attribute (<c>[Parameter]</c>, <c>[QueryProperty]</c>) is left unset.</item>
    /// </list>
    /// A property is eligible if it is decorated with one of the attributes registered in
    /// <see cref="IInjection.AttributeTypes"/> (e.g. [Parameter], [Inject]), or if its name is
    /// produced by a registered attribute's <c>getName</c> selector on the view's type (query properties).
    /// Failures on individual properties are logged to the debug output and do not abort injection.
    /// </summary>
    /// <param name="view">The instance to populate.</param>
    /// <param name="sp">The provider used for DI resolution and for the <see cref="IInjection"/> configuration.</param>
    /// <param name="parameters">Optional explicit values, keyed by property name (case-insensitive).</param>
    /// <returns>The same <paramref name="view"/> instance, for chaining.</returns>
    internal static TView Inject<[DynamicallyAccessedMembers(InjectableMembers.Properties)] TView>(this TView view, IServiceProvider sp, IDictionary<string, object?>? parameters = null)
    where TView : notnull
    {
        // Normalise keys so lookups are case-insensitive.
        parameters = parameters is null ? null : ToLastWins(parameters.Select(p => (p.Key.ToLowerInvariant(), p.Value)));

        // No injection configuration registered (or no attributes configured): nothing to do.
        var injection = sp.GetService<IInjection>();
        if (injection is null || injection.AttributeTypes.Count == 0)
            return view;

        // The type as the generator described it, else as reflection reads it (its static type is annotated to keep what that needs). Its type-level attributes (query attributes)
        // make properties injectable even without a property-level attribute.
        var injectable = InjectionTable.Resolve(sp, view);
        var queryPropertyNames = injectable.NamesGivenBy(injection);

        foreach (var property in injectable.Properties)
        {
            // "Context" is reserved (it holds the view's data context); a write-only property cannot be checked or read.
            if (property.Name == "Context" || property.Get is null)
                continue;

            // A RouteablesParameters property receives the entire parameter bag.
            if (parameters != null && property.PropertyType == typeof(RouteablesParameters))
            {
                var bag = new RouteablesParameters();
                foreach (var entry in parameters)
                    bag.Add(entry.Key, entry.Value);
                property.Set(view, bag);
                continue;
            }

            // Only a property decorated with a registered attribute ([Parameter], [Inject]) or named by a type-level one is touched.
            if (!injection.AttributeTypes.Any(entry => property.Has(entry.Type)) && !queryPropertyNames.Contains(property.Name))
                continue;

            // 1) An explicit value wins over anything else (Blazor semantics: null is a valid explicit value).
            if (parameters != null && parameters.TryGetValue(property.Name.ToLowerInvariant(), out var value))
            {
                TrySet(sp, property, view, value, "set parameter");
                continue;
            }

            // 2) Otherwise a property that is still null comes from the container, but only for an attribute that asks for it
            //    ([Inject]): a [Parameter] or a query property without a value stays unset.
            if (injection.AttributeTypes.Any(entry => entry.FromContainer && property.Has(entry.Type)) && property.Get(view) is null)
                TrySet(sp, property, view, () => sp.GetRequiredService(property.PropertyType), "inject");
        }

        return view;
    }

    // A property that cannot be set is reported to the debug output and does not stop the others.
    private static void TrySet(IServiceProvider provider, InjectableProperty property, object view, object? value, string action)
        => TrySet(provider, property, view, () => value, action);

    private static void TrySet(IServiceProvider provider, InjectableProperty property, object view, Func<object?> value, string action)
    {
        try
        {
            property.Set(view, value());
        }
        catch (Exception ex)
        {
            RouteableErrors.Report(provider, $"Failed to {action} '{property.Name}'", ex);
        }
    }

    /// <summary>
    /// Injects a view: the attribute entries routed to the view, plus the attribute entries routed to the view model
    /// (<c>{name::}</c> in a template, which sends the value to properties rather than to a constructor) whose name is the
    /// name of a property of the view decorated with a registered attribute ([Parameter], [QueryProperty], [RouteableParameter]).
    /// A value without such a property on the view is left to the view model, so a view never receives what it did not ask for.
    /// </summary>
    internal static TView InjectView<[DynamicallyAccessedMembers(InjectableMembers.Properties)] TView>(this TView view, IServiceProvider sp, IRouteableArgs args)
        where TView : notnull
    {
        var parameters = args.ViewParameters.ResolveAttributesParameters();

        var injection = sp.GetService<IInjection>();
        if (injection is not null && injection.AttributeTypes.Count > 0 && args.ViewModelParameters.Any(e => e.attribute && e.name is not null))
        {
            var names = view.InjectablePropertyNames(sp, injection);
            foreach (var entry in args.ViewModelParameters)
                if (entry.attribute && entry.name is not null && names.Contains(entry.name) && !parameters.ContainsKey(entry.name))
                    parameters[entry.name] = entry.value;
        }

        return view.Inject(sp, parameters);
    }

    // The properties a view declares as injectable, ignoring the ones that hold the whole bag or the data context.
    private static HashSet<string> InjectablePropertyNames<[DynamicallyAccessedMembers(InjectableMembers.Properties)] TView>(this TView view, IServiceProvider sp, IInjection injection)
        where TView : notnull
    {
        var injectable = InjectionTable.Resolve(sp, view);
        var names = injectable.NamesGivenBy(injection);

        foreach (var property in injectable.Properties)
        {
            if (property.Name == "Context" || property.PropertyType == typeof(RouteablesParameters))
                continue;
            if (injection.AttributeTypes.Any(a => property.Has(a.Type)))
                names.Add(property.Name);
        }

        return names;
    }

    /// <summary>
    /// Extracts the attribute-bound entries (e.g. query/[Parameter] values) as a name → value map.
    /// </summary>
    internal static Dictionary<string, object?> ResolveAttributesParameters(this IList<RouteableArgument> parameters)
        => ToLastWins(parameters.Where(p => p.attribute == true).Select(p => (p.name!, p.value)));

    // A name given twice (a value passed by hand and the one of the address, "Tab" and "tab") is not an error: the last one is used.
    private static Dictionary<string, object?> ToLastWins(IEnumerable<(string name, object? value)> pairs)
    {
        var result = new Dictionary<string, object?>();
        foreach (var (name, value) in pairs)
            result[name] = value;
        return result;
    }

    /// <summary>
    /// Extracts the positional (non-attribute) values, skipping nulls, to be passed as extra
    /// constructor arguments to <see cref="ActivatorUtilities"/>.
    /// </summary>
    private static object[] ResolveViewParameters(this IList<RouteableArgument> parameters)
        => parameters.Where(p => p.attribute == false && p.value is not null).Select(p => p.value!).ToArray();

    /// <summary>
    /// Gets the entry at <paramref name="index"/> cast to <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="index"/> is out of range.</exception>
    /// <exception cref="InvalidCastException">The value is null or not a <typeparamref name="T"/>.</exception>
    private static T Resolve<T>(this IList<RouteableArgument> args, int index)
    {
        if (index < 0 || index >= args.Count)
            throw new ArgumentException($"Invalid index : {index} but length is : {args.Count}");
        var value = args[index].value;
        return value is T t ? t :
            throw new InvalidCastException($"Excepted argument is type of : {typeof(T)} but resolved : {value?.GetType()}");
    }
}
