namespace Routeables.Abstraction;

/// <summary>
/// Lets a view model (or a view) load what it needs before it is shown. The router calls it on the target once it is built, with the
/// arguments of the route change and before anything of the current page is torn down. For data typed at compile time, see
/// <see cref="IInitializeAsync{TData}"/>.
/// </summary>
public interface IInitializeAsync
{
    /// <summary>Initializes the target with the arguments of the route change.</summary>
    /// <param name="args">The route, the values and the parameters of the route change.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    Task InitializeAsync(IRouteableArgs args, CancellationToken ct = default);
}

/// <summary>
/// Typed counterpart of <see cref="IInitializeAsync"/>: receives the strongly typed data passed by
/// <c>GoToRouteAsync&lt;TViewModel, TData&gt;</c>. The generic call only compiles when the target view model
/// implements <c>IInitializeAsync&lt;TData&gt;</c>, so the data type is checked at compile time.
/// </summary>
/// <typeparam name="TData">The type of the data supplied when routing.</typeparam>
/// <remarks>
/// Called after <see cref="IInitializeAsync.InitializeAsync"/>. The interface is contravariant, so a view model
/// implementing <c>IInitializeAsync&lt;object&gt;</c> accepts any data type.
/// </remarks>
public interface IInitializeAsync<in TData>
{
    /// <summary>
    /// Initializes the view model (or view) with the data supplied by the route change call.
    /// </summary>
    /// <param name="data">The data passed to <c>GoToRouteAsync&lt;TViewModel, TData&gt;</c>.</param>
    /// <param name="ct">A token to observe for cancellation.</param>
    Task InitializeAsync(TData data, CancellationToken ct = default);
}