using Routeables.Services;

namespace Routeables.Uno.Services;

/// <summary>Task dialogs, drawn by an <see cref="IMessageBoxService"/>.</summary>
public class RouteableTaskDialogService(IMessageBoxService service) : IRouteableTaskDialogService
{
    /// <inheritdoc />
    public Task<RouteableTaskDialogResult> ShowAsync(RouteableTaskDialog @params, params RouteableTaskDialogResult[] results)
    {
        if (@params is null)
            return Task.FromResult(RouteableTaskDialogResult.None);

        return UiThread.RunAsync(() => service.ShowAsync(@params, null, results));
    }
}
