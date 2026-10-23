using CommunityToolkit.Maui;
using Microsoft.Maui.Controls.Shapes;
using Routeables.Maui.Services;

namespace Reshapes.Maui.CommunityToolkit;

/// <summary>The chrome of a CommunityToolkit popup: the dialog's shape (see <see cref="RouteableNativeDialogLook"/>), no dismissal by tapping outside.</summary>
internal static class PopupLook
{
    public static PopupOptions Options() => new()
    {
        CanBeDismissedByTappingOutsideOfPopup = false,
        Shape = new RoundRectangle { CornerRadius = new CornerRadius(RouteableNativeDialogLook.CornerRadius), StrokeThickness = 0 }
    };
}
