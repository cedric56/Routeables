using Routeables.Services;

namespace Routeables.Uno.Services;

/// <summary>Dialogs that host a content, drawn by an <see cref="IMessageBoxService"/>. It also answers the simple message boxes with them.</summary>
public class RouteableContentDialogService(IMessageBoxService service) : RouteableContentDialogServiceBase
{
    /// <inheritdoc />
    public override Task<RouteableContentDialogResult> ShowAsync(RouteableContentDialog @params)
    {
        ArgumentNullException.ThrowIfNull(@params);
        return UiThread.RunAsync(() => service.ShowAsync(@params));
    }
}
