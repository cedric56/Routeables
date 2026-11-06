using Routeables.Abstraction;

namespace XAnimals.AngularServer;

/// <summary>The "view" of a route on this server: nothing. The state of the view model is what the front end receives.</summary>
internal sealed class AnimalsPage;

internal static class AnimalRoutes
{
    /// <summary>Registers the list of a species (<c>/cats</c>) and its detail (<c>/cats/{name}</c>).</summary>
    public static void RegisterAnimals<TList, TDetail>(this IServiceCollection services, string species)
        where TList : class
        where TDetail : class
    {
        services.RegisterRoute<AnimalsPage, TList>(route: $"/{species}");
        services.RegisterRoute<AnimalsPage, TDetail>(route: $"/{species}/{{name}}");
    }

    /// <summary>Registers a page the menu or a region opens by its name (<c>history</c>).</summary>
    public static void RegisterPage<TViewModel>(this IServiceCollection services, string route)
        where TViewModel : class
        => services.RegisterRoute<AnimalsPage, TViewModel>(route: route);
}
