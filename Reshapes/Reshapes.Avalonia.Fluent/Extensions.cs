using Microsoft.Extensions.DependencyInjection;
using Routeables.Avalonia.Services;

namespace Reshapes.Avalonia.Fluent;

public static class Extensions
{
    /// <summary>
    /// Draws the dialogs, content dialogs, task dialogs and notifications with FluentAvaloniaUI. Call it in the block of
    /// <c>UseRouteables</c>, in place of <c>UseRouteablesServices</c>.
    /// <code>
    /// AppBuilder.Configure&lt;App&gt;().UseRouteables&lt;MainViewModel&gt;(r => r.UseFluentAvalonia("avares://MyApp/Assets/icon.ico"));
    /// </code>
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="icon">The icon of the windows that are drawn.</param>
    public static IServiceCollection UseFluentAvalonia(this IServiceCollection services, string icon)
        => services.UseRouteablesServices(icon, new ReshapeMessageBoxService());
}
