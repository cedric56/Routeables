using Routeables.Abstraction;

namespace Routeables.Routing;

internal static class TypedInitialization
{
    /// <summary>Name of the <see cref="IRouteableArgs.Values"/> entry that holds the typed initializer.</summary>
    internal const string Key = "typed-initializer";

    /// <summary>
    /// Creates routing arguments carrying <paramref name="data"/>. The carrier goes in
    /// <see cref="IRouteableArgs.Values"/>, never in <see cref="IRouteableArgs.ViewModelParameters"/>,
    /// so it is not passed as an extra constructor argument to the view model.
    /// </summary>
    internal static IRouteableArgs CreateArgs<TData>(TData data) =>
        RouteableArgs.Create().ForValues(new RouteableArgument(new TypedInitializer<TData>(data), Key));

    /// <summary>Finds the typed initializer carried by <paramref name="args"/>, if any.</summary>
    internal static ITypedInitializer? Find(IRouteableArgs args) =>
        args.Values.Select(e => e.value).OfType<ITypedInitializer>().FirstOrDefault();
}

/// <summary>
/// Strongly typed routing: the data type is checked against the destination view model at compile time.
/// </summary>
public static class RouterTypedExtensions
{
    /// <summary>
    /// Goes to <typeparamref name="TViewModel"/> and hands it <paramref name="data"/> through
    /// <see cref="IInitializeAsync{TData}"/>. Does not compile if the view model cannot accept
    /// <typeparamref name="TData"/>.
    /// </summary>
    /// <typeparam name="TViewModel">The destination view model; must implement <c>IInitializeAsync&lt;TData&gt;</c>.</typeparam>
    /// <typeparam name="TData">The type of the data passed to the view model.</typeparam>
    /// <param name="router">The router.</param>
    /// <param name="route">The concrete route.</param>
    /// <param name="data">The data handed to <see cref="IInitializeAsync{TData}.InitializeAsync"/>.</param>
    /// <param name="template">The route template; found from the route when omitted.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <param name="viewKeys">Optional view keys, as for <see cref="IRouter.GoToRouteAsync{TViewModel}(string, IRouteableArgs?, string?, CancellationToken, string[])"/>.</param>
    /// <returns>The route change result.</returns>
    public static Task<RouteableResult> GoToRouteAsync<TViewModel, TData>(
        this IRouter router,
        string route,
        TData data,
        string? template = null,
        CancellationToken cancellationToken = default,
        params string[] viewKeys)
        where TViewModel : class, IInitializeAsync<TData>
    {
        ArgumentNullException.ThrowIfNull(router);
        return router.GoToRouteAsync<TViewModel>(
            route, TypedInitialization.CreateArgs(data), template, cancellationToken, viewKeys);
    }
}