using Routeables.Abstraction;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Net;
using Routeables.Services;

namespace Reshapes.Razor.MudBlazor;

/// <summary>
/// Provides a concrete implementation of <see cref="IRouteableNotificationService"/> using MudBlazor's <see cref="ISnackbar"/> service 
/// to display toast notifications with sanitized titles, messages, severity levels, and interaction event handlers.
/// </summary>
/// <param name="SnackbarService">The underlying MudBlazor snackbar service.</param>
internal class ReshapeNotificationService(ISnackbar SnackbarService) : IRouteableNotificationService
{
    /// <summary>
    /// Displays a toast notification with the specified title, message, type, expiration duration, and interaction handlers.
    /// </summary>
    /// <param name="title">The notification title.</param>
    /// <param name="message">The notification body message.</param>
    /// <param name="type">The type classification of the notification, determining its visual severity.</param>
    /// <param name="expiration">Optional duration before the notification automatically dismisses; if null, user interaction is required.</param>
    /// <param name="onClick">Optional callback invoked when the notification is clicked.</param>
    /// <param name="onClose">Optional callback invoked when the notification is closed or dismissed.</param>
    public void Show(string title, string message, RouteableNotificationType type = RouteableNotificationType.Information, TimeSpan? expiration = null, Action? onClick = null, Action? onClose = null)
    {
        // Encode untrusted values BEFORE building markup.
        var safeTitle = WebUtility.HtmlEncode(title ?? string.Empty);
        var safeMessage = WebUtility.HtmlEncode(message ?? string.Empty);

        var snack = SnackbarService.Add(
            new MarkupString($"<h5>{safeTitle}</h5>{safeMessage}"),
            GetSeverity(type),
            config =>
            {
                config.RequireInteraction = expiration is null;
                if (expiration.HasValue)
                    config.VisibleStateDuration = (int)expiration.Value.TotalMilliseconds;
                config.OnClick = snackbar =>
                {
                    RouteableErrors.Guard(null, RouteableCallbackSources.ToastOnClick, onClick);
                    return Task.CompletedTask;
                };
            });

        if (snack is not null)
        {
            Action<Snackbar>? closeHandler = null;
            closeHandler = (snackbar) =>
            {
                snack.OnClose -= closeHandler;   // detach immediately, fire-once
                RouteableErrors.Guard(null, RouteableCallbackSources.ToastOnClose, onClose);
            };
            snack.OnClose += closeHandler;
        }
    }

    /// <summary>
    /// Maps a <see cref="RouteableNotificationType"/> domain enum value to its corresponding MudBlazor <see cref="Severity"/> level.
    /// </summary>
    /// <param name="type">The notification type.</param>
    /// <returns>The corresponding MudBlazor severity level.</returns>
    private Severity GetSeverity(RouteableNotificationType type)
    {
        return type switch
        {
            RouteableNotificationType.Information => Severity.Info,
            RouteableNotificationType.Success => Severity.Success,
            RouteableNotificationType.Warning => Severity.Warning,
            RouteableNotificationType.Error => Severity.Error,
            _ => Severity.Normal
        };
    }
}