using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Routeables.Abstraction.Guards;
using XAnimals.Common;
using XAnimals.Razor.Session;
using XAnimals.ViewModels;

namespace XAnimals.Razor;

public static class Extensions
{
    /// <summary>
    /// Everything the pages need besides <c>UseRouteables</c>: the view models of the sample, the services that a web page
    /// replaces (dialogs, notifications) and the sessions that keep the routers of a visitor between two requests.
    /// </summary>
    public static IServiceCollection AddAnimalsWeb(this IServiceCollection services)
    {
        AnimalIcons.Register();   // the glyph of each page in the menu, as in the Blazor sample
        services.RegisterViewModels();
        // RegisterViewModels() makes the sign-in a singleton, as a desktop has one user. A site has many: each session has its own.
        services.Replace(ServiceDescriptor.Scoped<MemberSession, MemberSession>());
        services.Replace(ServiceDescriptor.Scoped<IRouteGuard, MembersGuard>());

        return services.AddVisitorSessions();
    }
}
