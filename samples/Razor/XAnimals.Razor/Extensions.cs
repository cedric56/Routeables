using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Abstraction;
using XAnimals.Razor.Session;
using XAnimals.ViewModels;

namespace XAnimals.Razor;

public static class Extensions
{
    /// <summary>
    /// Everything the pages need besides <c>AddRouteablesAspNetCore</c>: the view models of the sample, the services that a web page
    /// replaces (dialogs, notifications) and the sessions that keep the routers of a visitor between two requests.
    /// </summary>
    public static IServiceCollection AddAnimalsWeb(this IServiceCollection services)
    {
        services.RegisterViewModels();
        // RegisterViewModels() makes the sign-in a singleton, as a desktop has one user. A site has many: each session has its own.
        services.Replace(ServiceDescriptor.Scoped<MemberSession, MemberSession>());
        services.Replace(ServiceDescriptor.Scoped<IRouteGuard, MembersGuard>());

        return services.AddVisitorSessions();
    }
}
