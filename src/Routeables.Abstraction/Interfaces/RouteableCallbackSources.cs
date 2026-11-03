namespace Routeables.Abstraction;

/// <summary>
/// The sources the callbacks of the application are reported under when they throw (see <see cref="RouteableErrors.Guard(IServiceProvider?, string, Action?)"/>):
/// one name for each callback, shared by every UI framework, so an error sink can filter on it.
/// </summary>
public static class RouteableCallbackSources
{
    /// <summary>A dialog is about to open.</summary>
    public const string DialogOpening = "Dialog.Opening";
    /// <summary>A dialog has opened.</summary>
    public const string DialogOpened = "Dialog.Opened";
    /// <summary>A dialog has closed.</summary>
    public const string DialogClosed = "Dialog.Closed";
    /// <summary>A task dialog is closing.</summary>
    public const string TaskDialogClosing = "TaskDialog.Closing";
    /// <summary>A content dialog is closing.</summary>
    public const string ContentDialogClosing = "ContentDialog.Closing";
    /// <summary>A button of a content dialog was clicked.</summary>
    public const string ContentDialogClick = "ContentDialog.Click";
    /// <summary>The primary button of a content dialog was clicked.</summary>
    public const string ContentDialogPrimaryButtonClick = "ContentDialog.PrimaryButtonClick";
    /// <summary>The secondary button of a content dialog was clicked.</summary>
    public const string ContentDialogSecondaryButtonClick = "ContentDialog.SecondaryButtonClick";
    /// <summary>The close button of a content dialog was clicked.</summary>
    public const string ContentDialogCloseButtonClick = "ContentDialog.CloseButtonClick";
    /// <summary>A notification was tapped.</summary>
    public const string NotificationClick = "Notification.Click";
    /// <summary>A notification closed.</summary>
    public const string NotificationClose = "Notification.Close";
    /// <summary>A toast was tapped.</summary>
    public const string ToastOnClick = "Toast.OnClick";
    /// <summary>A toast closed.</summary>
    public const string ToastOnClose = "Toast.OnClose";
}
