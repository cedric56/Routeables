using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using System.Diagnostics.CodeAnalysis;

namespace Routeables.Routing;

/// <summary>
/// One place to declare what the application can show: the pages, the tabs of the menu and the modals. Each declaration is made of the
/// registrations that exist on their own (<c>RegisterRoute</c>, <c>AddMenuEntries</c>); this builder only puts them side by side, so a
/// screen is declared once whatever way it is reached.
/// </summary>
/// <example>
/// <code>
/// services.AddRoutes(nav => nav
///     .Tab&lt;HomeView, HomeViewModel&gt;("Home", route: "/home")
///     .Page&lt;OrderView, OrderViewModel&gt;("/orders/{id:int}")
///     .Modal&lt;ConfirmView, ConfirmViewModel&gt;());
/// </code>
/// </example>
public sealed class RouteBuilder
{
    private readonly IServiceCollection _services;

    internal RouteBuilder(IServiceCollection services) => _services = services;

    /// <summary>
    /// Declares a page reached by a route: <c>router.GoToAsync("/orders/42")</c>, a link, a deep link, or without any string
    /// <c>router.GoToAsync&lt;OrderViewModel&gt;(values)</c>.
    /// </summary>
    /// <typeparam name="TView">The view.</typeparam>
    /// <typeparam name="TViewModel">The view model.</typeparam>
    /// <param name="route">The route template, with its placeholders and constraints (<c>/orders/{id:int}</c>).</param>
    /// <returns>This builder.</returns>
    public RouteBuilder Page<
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TView,
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TViewModel>(string route)
        where TView : class where TViewModel : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        _services.RegisterRoute<TView, TViewModel>(route: route);
        return this;
    }

    /// <summary>
    /// Declares a tab: a page, and its entry in the menu of the main view model (<see cref="IMainViewModel.Entries"/>).
    /// </summary>
    /// <typeparam name="TView">The view.</typeparam>
    /// <typeparam name="TViewModel">The view model.</typeparam>
    /// <param name="displayName">The text of the entry.</param>
    /// <param name="route">The route of the tab; <c>/home</c> for a <c>HomeViewModel</c> when omitted.</param>
    /// <param name="icon">The icon of the entry, or <see langword="null"/>.</param>
    /// <param name="description">A line under the name, or <see langword="null"/>.</param>
    /// <param name="index">The place in the menu, or <see langword="null"/> for the end.</param>
    /// <returns>This builder.</returns>
    public RouteBuilder Tab<
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TView,
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TViewModel>(
        string displayName, string? route = null, string? icon = null, string? description = null, int? index = null)
        where TView : class where TViewModel : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var template = route ?? RouteableKeys.GetTemplate(typeof(TViewModel));
        _services.RegisterRoute<TView, TViewModel>(route: template);
        _services.AddMenuEntries(() => [new RouteableMenuEntry<TViewModel>(displayName, icon, route, template, description)], index);
        return this;
    }

    /// <summary>
    /// Declares a modal: a view that is shown over the page and answers with a result, <c>provider.ShowModalAsync&lt;TViewModel, TResult&gt;()</c>.
    /// The view implements <c>IRouteableModalFor&lt;TViewModel, TResult&gt;</c>. It has no route and no entry in the menu.
    /// </summary>
    /// <typeparam name="TView">The view of the modal.</typeparam>
    /// <typeparam name="TViewModel">The view model of the modal.</typeparam>
    /// <returns>This builder.</returns>
    public RouteBuilder Modal<
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TView,
        [DynamicallyAccessedMembers(InjectableMembers.Created)] TViewModel>()
        where TView : class where TViewModel : class
    {
        _services.RegisterRoute<TView, TViewModel>();
        return this;
    }
}

/// <summary>Registers the routes of an application in one place.</summary>
public static class RouteBuilderExtensions
{
    /// <summary>
    /// Declares the pages, tabs and modals of the application with a <see cref="RouteBuilder"/>. The router and the arguments
    /// factory are registered if no one did it yet (a host does it with <c>UseRouteables</c>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Declares the screens.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddRoutes(this IServiceCollection services, Action<RouteBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        if (!services.Any(d => d.ServiceType == typeof(IRouter)))
            services.RegisterRouting();
        if (!services.Any(d => d.ServiceType == typeof(IRouteableArgsFactory)))
            services.RegisterRouteableArgsFactory(() => RouteableArgs.Create());
        configure(new RouteBuilder(services));
        return services;
    }
}
