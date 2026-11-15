#if AVALONIA
using Avalonia.Threading;
#endif
using Routeables.Services;

// One file for every host that draws its dialogs: Routeables.Wpf and Routeables.Avalonia link it (and define WPF or AVALONIA), Routeables.Uno and
// Routeables.WinUI3 compile this folder. What a message means is in RouteableMessageBoxDialogService; here is only how each host goes to its
// interface thread, which window owns the dialog, and the name of its IMessageBoxService methods.
#if WPF
namespace Routeables.Wpf.Services;
#elif AVALONIA
namespace Routeables.Avalonia.Services;
#elif WINUI3
namespace Routeables.WinUI3.Services;
#else
namespace Routeables.Uno.Services;
#endif

/// <summary>Message boxes, drawn by an <see cref="IMessageBoxService"/>. The indexes returned follow the order of the buttons: Yes, No, then Cancel or Abort; -1 when the dialog was dismissed.</summary>
public class RouteableDialogService(IMessageBoxService service) : RouteableMessageBoxDialogService
{
    /// <inheritdoc />
    protected override Task<T> RunOnInterfaceThreadAsync<T>(Func<Task<T>> action)
#if WPF
        => UiThread.InvokeAsync(action);
#elif AVALONIA
        => Dispatcher.UIThread.Invoke(action);
#else
        => UiThread.RunAsync(action);
#endif

    /// <inheritdoc />
    protected override Task<string?> ShowMessageBoxAsync(string title, string message, string buttonText)
#if WPF
        => service.ShowMessageBoxAsync(title, message, buttonText, RouteableWindows.Active);
#elif AVALONIA
        => service.ShowMessageBoxAsync(title, message, buttonText, TopLevelStateManager.Default.GetActive());
#else
        => service.ShowMessageBoxAsync(title, message, buttonText);
#endif

    /// <inheritdoc />
    protected override Task<RouteableContentDialogResult> ShowContentAsync(RouteableContentDialog @params)
#if WPF
        => service.ShowAsync(@params, RouteableWindows.Active);
#elif AVALONIA
        => service.ShowContentDialogAsync(@params, TopLevelStateManager.Default.GetActive());
#else
        => service.ShowAsync(@params);
#endif

    /// <inheritdoc />
    protected override Task<RouteableTaskDialogResult> ShowTaskAsync(RouteableTaskDialog @params, RouteableTaskDialogResult[] results)
#if WPF
        => service.ShowAsync(@params, RouteableWindows.Active, results);
#elif AVALONIA
        => service.ShowTaskDialogAsync(@params, TopLevelStateManager.Default.GetActive(throwOnNull: true), results);
#else
        => service.ShowAsync(@params, null, results);
#endif
}
