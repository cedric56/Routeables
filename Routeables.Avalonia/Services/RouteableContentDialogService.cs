using Avalonia.Threading;
using Routeables.Services;

namespace Routeables.Avalonia.Services;

/// <summary>Dialogs that host a content, drawn by an <see cref="IMessageBoxService"/>. It also answers the simple message boxes with them.</summary>
public class RouteableContentDialogService(IMessageBoxService service) : RouteableContentDialogServiceBase
{
    /// <inheritdoc />
    public override async Task<RouteableContentDialogResult> ShowAsync(RouteableContentDialog @params)
    {
        return await Dispatcher.UIThread.Invoke(async () =>
        {
            return await service.ShowAsync(@params, TopLevelStateManager.Default.GetActive());
        });
    }
}
