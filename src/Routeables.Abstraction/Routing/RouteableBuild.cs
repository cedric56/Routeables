using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Routeables.Abstraction;

/// <summary>
/// What the code the source generator writes calls to finish what it built: the injection of the attributes, and the description of
/// the types it injects. It is meant for the generated code, not for the application.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class RouteableBuild
{
    /// <summary>Injects a view as <see cref="Extensions"/> does for the views it builds itself: its attribute entries, and the ones routed to its view model it asks for.</summary>
    public static T InjectView<[DynamicallyAccessedMembers(InjectableMembers.Properties)] T>(T view, IServiceProvider provider, IRouteableArgs args) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(args);
        return Extensions.InjectView(view, provider, args);
    }

    /// <summary>Injects a view model with the attribute entries routed to it.</summary>
    public static T InjectViewModel<[DynamicallyAccessedMembers(InjectableMembers.Properties)] T>(T viewModel, IServiceProvider provider, IRouteableArgs args) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(args);
        return viewModel.Inject(provider, Extensions.ResolveAttributesParameters(args.ViewModelParameters));
    }

    /// <summary>Tells the injection how <paramref name="type"/> is filled, so that it is not read by reflection.</summary>
    public static void Describe(IServiceCollection services, Type type, InjectableType injectable)
    {
        ArgumentNullException.ThrowIfNull(services);
        var table = services.Select(d => d.ImplementationInstance).OfType<InjectionTable>().FirstOrDefault();
        if (table is null)
        {
            table = new InjectionTable();
            services.AddSingleton(table);
        }
        table.Add(type, injectable);
    }
}
