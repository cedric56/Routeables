namespace Routeables.Services;

/// <summary>
/// The dialogs of the application behind one injection: ask for <see cref="IRouteableDialogs"/> instead of the three services.
/// <code>
/// public sealed class OrderViewModel(IRouter router, IRouteableDialogs dialogs)
/// {
///     public async Task DeleteAsync()
///     {
///         if (await dialogs.MessageBox.ShowYesNoAsync("Delete this order?"))
///             await router.GoBackAsync();
///     }
/// }
/// </code>
/// Each property is the service the host registered, or the one an application or a Reshape replaced it with.
/// </summary>
public interface IRouteableDialogs
{
    /// <summary>Message boxes: ok, yes/no, ok/cancel, errors.</summary>
    IRouteableDialogService MessageBox { get; }

    /// <summary>Dialogs that show a view and its view model.</summary>
    IRouteableContentDialogService ContentDialog { get; }

    /// <summary>Dialogs with a main instruction, a content and several buttons.</summary>
    IRouteableTaskDialogService TaskDialog { get; }
}
