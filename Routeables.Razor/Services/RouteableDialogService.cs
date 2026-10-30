using Routeables.Services;

namespace Routeables.Razor.Services;

/// <summary>
/// The <see cref="IRouteableDialogService"/> of a Razor application: it asks an <see cref="IMessageBoxService"/>
/// (<see cref="DefaultMessageBoxService"/> unless the application gave another one) to draw each dialog. The indexes returned follow
/// the order of the buttons: Yes, No, then Cancel or Abort; the last button when the dialog was dismissed.
/// </summary>
public class RouteableDialogService(IMessageBoxService service) : RouteableDialogServiceBase
{
    /// <inheritdoc />
    public override Task<int> ShowMessageAsync(string message, string title, string buttonText)
        => service.ShowMessageAsync(title, message, buttonText);

    /// <inheritdoc />
    public override Task<RouteableContentDialogResult> ShowContentDialogAsync(RouteableContentDialog @params)
    {
        ArgumentNullException.ThrowIfNull(@params);
        return service.ShowContentDialogAsync(@params);
    }

    /// <inheritdoc />
    public override Task<RouteableTaskDialogResult> ShowTaskDialogAsync(RouteableTaskDialog @params, params RouteableTaskDialogResult[] results)
    {
        ArgumentNullException.ThrowIfNull(@params);
        return service.ShowTaskDialogAsync(@params, results);
    }
}
