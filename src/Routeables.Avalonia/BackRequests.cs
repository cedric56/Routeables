using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Routeables.Services;

namespace Routeables.Avalonia;

// The Back request of the system, answered by the router: the button or gesture of Android (TopLevel.BackRequested), the mouse back
// button and Alt+Left of a desktop. A browser has its own back button for its own history.
internal static class BackRequests
{
    public static void Attach(Control view, IServiceProvider provider)
    {
        if (TopLevel.GetTopLevel(view) is { } topLevel)
        {
            Hook(topLevel, provider);
            return;
        }

        // A view of a single-view platform is attached later, when the platform shows it.
        void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            view.AttachedToVisualTree -= OnAttached;
            if (TopLevel.GetTopLevel(view) is { } attached)
                Hook(attached, provider);
        }

        view.AttachedToVisualTree += OnAttached;
    }

    private static void Hook(TopLevel topLevel, IServiceProvider provider)
    {
        topLevel.AddHandler(TopLevel.BackRequestedEvent, (object? sender, RoutedEventArgs e) =>
        {
            if (!e.Handled && RouteableBack.TryGoBack(provider))
                e.Handled = true;
        });

        if (OperatingSystem.IsBrowser())
            return;

        topLevel.AddHandler(InputElement.PointerPressedEvent, (object? sender, PointerPressedEventArgs e) =>
        {
            if (!e.Handled
                && e.GetCurrentPoint(topLevel).Properties.PointerUpdateKind == PointerUpdateKind.XButton1Pressed
                && RouteableBack.TryGoBack(provider))
                e.Handled = true;
        }, RoutingStrategies.Tunnel);

        topLevel.AddHandler(InputElement.KeyDownEvent, (object? sender, KeyEventArgs e) =>
        {
            if (!e.Handled && e.Key == Key.Left && e.KeyModifiers == KeyModifiers.Alt && RouteableBack.TryGoBack(provider))
                e.Handled = true;
        }, RoutingStrategies.Tunnel);
    }
}
