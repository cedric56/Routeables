using Avalonia.Threading;
using Routeables.Services;
using Routeables.Avalonia;

namespace Routeables.Avalonia.Services;

public class RouteableTaskDialogService(IMessageBoxService service) : IRouteableTaskDialogService
{
    public async Task<RouteableTaskDialogResult> ShowAsync(RouteableTaskDialog @params, params RouteableTaskDialogResult[] results)
    {
        if (@params != null)
        {
            return await Dispatcher.UIThread.Invoke(async () =>
            {
                return await service.ShowAsync(@params, TopLevelStateManager.Default.GetActive(throwOnNull: true), results);
            });
        }

        return RouteableTaskDialogResult.None;
    }
}
