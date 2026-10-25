using Routeables.Services;

namespace Routeables.Wpf.Services;

/// <summary>Shows a content dialog for <c>IRouteableContentDialogService</c>, through the <see cref="IMessageBoxService"/>.</summary>
public class RouteableContentDialogService(IMessageBoxService service) : RouteableContentDialogServiceBase
{
    /// <inheritdoc />
    public override Task<RouteableContentDialogResult> ShowAsync(RouteableContentDialog @params)
        => UiThread.InvokeAsync(() => service.ShowAsync(@params, RouteableWindows.Active));
}
