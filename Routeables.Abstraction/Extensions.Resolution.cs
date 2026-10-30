using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Routeables.Abstraction;

/// <summary>Resolution of views, view models and modals from the container.</summary>
public static partial class Extensions
{
    // Weak tracking is for what nobody owns (transient pages). A singleton or scoped page belongs to its cache, which
    // disposes it: tracking it too would dispose it twice.
    private static void TrackUnlessCached(this IServiceProvider provider, object? instance)
    {
        if (instance is null) return;
        if (provider.GetService<ScopedViewCache>()?.Owns(instance) == true) return;
        if (provider.GetService<SingletonViewCache>()?.Owns(instance) == true) return;
        provider.GetService<WeakDisposableTracker>()?.Track(instance);
    }

    /// <summary>
    /// Resolves a view model through the keyed factory registered for <paramref name="route"/>
    /// and tracks it for weak disposal.
    /// </summary>
    /// <param name="provider">The provider passed to the factory (determines scope).</param>
    /// <param name="route">The view-model registration key.</param>
    /// <param name="args">Arguments for the factory; a new empty <see cref="IRouteableArgs"/> when omitted.</param>
    /// <exception cref="InvalidOperationException">Nothing is registered for the key, or the factory returned null.</exception>
    public static object? GetViewModel(this IServiceProvider provider, string route, IRouteableArgs? args = null)
    {
        var factory = provider.FindFactory(route, out var factoryProvider);
        if (factory != null)
        {
            var instance = factory(factoryProvider, args ?? provider.GetRequiredService<IRouteableArgsFactory>().New());
            provider.TrackUnlessCached(instance);

            return instance;
        }
        return null;
    }

    /// <summary>
    /// Resolves a view through the keyed factory registered for <paramref name="key"/>
    /// and tracks it for weak disposal.
    /// </summary>
    /// <param name="provider">The provider passed to the factory (determines scope).</param>
    /// <param name="key">The view registration key.</param>
    /// <param name="args">Arguments for the factory; a new empty <see cref="IRouteableArgs"/> when omitted.</param>
    /// <exception cref="InvalidOperationException">No view is registered for the key, or the factory returned null.</exception>
    internal static object? GetView(this IServiceProvider provider, object key, IRouteableArgs? args = null)
    {
        var factory = provider.FindFactory(key, out var factoryProvider);
        if (factory != null)
        {
            var instance = factory(factoryProvider, args ?? provider.GetRequiredService<IRouteableArgsFactory>().New())
            ?? throw new InvalidOperationException($"Unable to create view for {key}. Ensure that it is registered with the service provider.");

            // Track with weak reference
            provider.TrackUnlessCached(instance);

            return instance;
        }
        return null;
    }

    /// <summary>
    /// Resolves the view for <typeparamref name="TViewModel"/>, keyed by the view model's type name.
    /// </summary>
    public static IRouteableFor? GetContextFor<TViewModel>(this IServiceProvider provider, IRouteableArgs? args = null)
    {
        var keys = RouteableKeys.GetKeys(typeof(TViewModel));
        return provider.GetView(keys.viewKey, args) as IRouteableFor;
    }

    /// <summary>
    /// Creates the content registered under <paramref name="key"/>, passing
    /// <paramref name="context"/> as the single view parameter (under <c>Constants.Key</c>).
    /// Unlike <see cref="GetView"/>, the result is not tracked for disposal.
    /// </summary>
    /// <param name="provider">The provider passed to the factory.</param>
    /// <param name="key">The registration key.</param>
    /// <param name="context">The context object handed to the factory.</param>
    /// <exception cref="InvalidOperationException">No view is registered for the key, or the factory returned null.</exception>
    public static object GetContent(this IServiceProvider provider, object key, object context)
    {
        var factory = provider.FindFactory(key, out var factoryProvider)
            ?? throw new InvalidOperationException($"No such view registered: {key}");

        return factory(factoryProvider, provider.GetRequiredService<IRouteableArgsFactory>().New().ForView(
            new RouteableArgument(context, Constants.Key)))
            ?? throw new InvalidOperationException($"Unable to create view for {key}. Ensure that it is registered with the service provider.");
    }

    /// <summary>
    /// Resolves the view for <typeparamref name="TViewModel"/> as a modal returning
    /// <typeparamref name="TResult"/>. A view that is not that modal (its result type is another one, say) throws an exception
    /// that names the types.
    /// </summary>
    /// <param name="provider">The provider used to resolve the view.</param>
    /// <param name="args">Optional routing arguments.</param>
    /// <param name="route">Optional view key; defaults to the view model's type name.</param>
    /// <exception cref="InvalidOperationException">The view's result type mismatches, or it is not a modal for these types.</exception>
    public static IRouteableModalFor<TViewModel, TResult>? GetModalFor<TViewModel, TResult>(
        this IServiceProvider provider, IRouteableArgs? args = null, string? route = null)
        where TViewModel : class
    {
        var view = provider.GetView(route ?? typeof(TViewModel).Name, args);

        // The closed interface is the whole check: a view whose result type is another one is not this modal.
        return view as IRouteableModalFor<TViewModel, TResult>
            ?? throw new InvalidOperationException(
                $"The view associated with the view model {typeof(TViewModel).Name} ({view?.GetType().Name ?? "none"}) is not a " +
                $"{nameof(IRouteableModalFor<TViewModel, TResult>)}<{typeof(TViewModel).Name}, {typeof(TResult).Name}>: check its result type.");
    }
}
