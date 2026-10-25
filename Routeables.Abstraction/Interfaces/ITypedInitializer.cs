using Routeables.Abstraction;

namespace Routeables.Routing;

/// <summary>
/// Carries typed routing data from <c>GoToRouteAsync&lt;TViewModel, TData&gt;</c> to
/// <see cref="RouterLifecycle.InitializeAsync"/> without reflection (AOT friendly): the generic
/// <see cref="TypedInitializer{TData}"/> knows <c>TData</c>, the lifecycle only sees this interface.
/// </summary>
internal interface ITypedInitializer
{
    /// <summary>Gets whether <paramref name="target"/> implements <c>IInitializeAsync&lt;TData&gt;</c>.</summary>
    bool Accepts(object target);

    /// <summary>Calls <c>IInitializeAsync&lt;TData&gt;.InitializeAsync</c> on <paramref name="target"/>.</summary>
    Task InitializeAsync(object target, CancellationToken ct);
}

internal sealed class TypedInitializer<TData>(TData data) : ITypedInitializer
{
    public bool Accepts(object target) => target is IInitializeAsync<TData>;

    public Task InitializeAsync(object target, CancellationToken ct) =>
        ((IInitializeAsync<TData>)target).InitializeAsync(data, ct);
}