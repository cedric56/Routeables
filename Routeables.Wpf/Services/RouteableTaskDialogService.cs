using Routeables.Services;

namespace Routeables.Wpf.Services;

/// <summary>Shows a task dialog for <c>IRouteableTaskDialogService</c>, through the <see cref="IMessageBoxService"/>.</summary>
public class RouteableTaskDialogService(IMessageBoxService service) : IRouteableTaskDialogService
{
    /// <inheritdoc />
    public Task<RouteableTaskDialogResult> ShowAsync(RouteableTaskDialog @params, params RouteableTaskDialogResult[] results)
    {
        if (@params is null)
            return Task.FromResult(RouteableTaskDialogResult.None);

        return UiThread.InvokeAsync(() => service.ShowAsync(@params, RouteableWindows.Active, results));
    }
}
