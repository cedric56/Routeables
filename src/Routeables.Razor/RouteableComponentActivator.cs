using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Components;

namespace Routeables.Razor;

/// <summary>
/// Lets Blazor render the view that the router created, instead of building a second one: the router initializes a view
/// (<c>InitializeAsync</c>, <c>OnEnteringAsync</c>) and keeps it in its history, and until now Blazor rendered another instance of the
/// same component next to it, so the hooks written in the view ran on an instance that was never displayed.
/// <para>
/// A view that is about to be rendered for the first time is <em>offered</em> (by <see cref="RouteableFor.Render"/>); when Blazor asks for a
/// component of that type, the offered instance is handed over. A view that was already rendered once cannot be attached to a renderer
/// again (a component has one render handle for life): when it is shown again, for example by Back, Blazor builds a new instance as it always did.
/// Any other component is built the way Blazor builds it.
/// </para>
/// </summary>
public sealed class RouteableComponentActivator : IComponentActivator
{
    private readonly object _gate = new();
    private readonly List<RouteableFor> _offered = [];

    internal void Offer(RouteableFor view)
    {
        if (view.Claimed)
            return;
        lock (_gate)
        {
            if (!_offered.Contains(view))
                _offered.Add(view);
        }
    }

    internal void Withdraw(RouteableFor view)
    {
        lock (_gate)
            _offered.Remove(view);
    }

    /// <inheritdoc />
    public IComponent CreateInstance([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        lock (_gate)
        {
            var index = _offered.FindIndex(v => v.GetType() == componentType);
            if (index >= 0)
            {
                var view = _offered[index];
                _offered.RemoveAt(index);
                view.Claimed = true;
                return view;
            }
        }

        return Build(componentType);
    }

    // The way Blazor builds a component: a public constructor without parameters. The exception of a constructor reaches the caller as it
    // was thrown, not wrapped in a TargetInvocationException; a type that cannot be built says which one it is.
    private static IComponent Build([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type componentType)
    {
        if (!typeof(IComponent).IsAssignableFrom(componentType))
            throw new ArgumentException($"{componentType} does not implement {nameof(IComponent)}.", nameof(componentType));

        try
        {
            return (IComponent)System.Activator.CreateInstance(componentType)!;
        }
        catch (MissingMethodException ex)
        {
            throw new InvalidOperationException($"Cannot create a component of type {componentType}: it needs a public constructor without parameters.", ex);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
            throw;   // not reached
        }
    }
}
