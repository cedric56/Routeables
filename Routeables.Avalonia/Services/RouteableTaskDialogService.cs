using Avalonia.Threading;
using Routeables.Services;
using Routeables.Avalonia;

namespace Routeables.Avalonia.Services;

/// <summary>Task dialogs (a header, buttons and progress) of Avalonia, drawn by the message box service and shown over the active window.</summary>
/// <param name="service">The message box service that draws them.</param>
public class RouteableTaskDialogService(IMessageBoxService service) : IRouteableTaskDialogService
{
    /// <inheritdoc />
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
