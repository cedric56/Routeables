namespace Routeables.Abstraction;

/// <summary>
/// The sources the callbacks of the application are reported under when they throw (see <see cref="RouteableErrors.Guard(IServiceProvider?, string, Action?)"/>):
/// one name for each callback, shared by every UI framework, so an error sink can filter on it.
/// </summary>
public static class RouteableCallbackSources
{
    public const string DialogOpening = "Dialog.Opening";
    public const string DialogOpened = "Dialog.Opened";
    public const string DialogClosed = "Dialog.Closed";
    public const string TaskDialogClosing = "TaskDialog.Closing";
    public const string ContentDialogClosing = "ContentDialog.Closing";
    public const string ContentDialogClick = "ContentDialog.Click";
    public const string ContentDialogPrimaryButtonClick = "ContentDialog.PrimaryButtonClick";
    public const string ContentDialogSecondaryButtonClick = "ContentDialog.SecondaryButtonClick";
    public const string ContentDialogCloseButtonClick = "ContentDialog.CloseButtonClick";
    public const string NotificationClick = "Notification.Click";
    public const string NotificationClose = "Notification.Close";
    public const string ToastOnClick = "Toast.OnClick";
    public const string ToastOnClose = "Toast.OnClose";
}
