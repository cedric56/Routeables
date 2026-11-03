namespace Routeables.Services;

/// <summary>
/// Describes the content, header, icon, and lifecycle callbacks for a
/// single <see cref="IRouteableDialogService.ShowTaskDialogAsync(Routeables.Services.RouteableTaskDialog, Routeables.Services.RouteableTaskDialogResult[])"/> call. Mirrors the
/// shape of WinUI's TaskDialog so platform adapters can map these
/// properties directly onto their native dialog control.
/// </summary>
public class RouteableTaskDialog
{
    /// <summary>
    /// Gets or sets the title text shown in the dialog's title bar/window chrome.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the large, prominent heading shown above the content area.
    /// </summary>
    public string? Header { get; set; }

    /// <summary>
    /// Gets or sets the smaller heading shown below <see cref="Header"/>,
    /// typically used for supporting/explanatory text.
    /// </summary>
    public string? SubHeader { get; set; }

    /// <summary>
    /// Gets or sets the body content of the dialog. Typically a string,
    /// but platform adapters may also accept a view/control instance here
    /// for richer dialog bodies.
    /// </summary>
    public object? Content { get; set; }

    /// <summary>
    /// Gets or sets the icon shown next to <see cref="Header"/>. The
    /// accepted type is platform-defined (e.g. a glyph key, an
    /// <c>IImage</c>, or a platform-native icon type).
    /// </summary>
    public object? IconSource { get; set; }

    /// <summary>
    /// Gets or sets whether an indeterminate progress bar is shown in the dialog.
    /// </summary>
    public bool ShowProgressBar { get; set; }

    /// <summary>
    /// Gets or sets whether/when the <see cref="Footer"/> is visible.
    /// </summary>
    public RouteableTaskDialogFooterVisibility FooterVisibility { get; set; }

    /// <summary>
    /// Gets or sets whether the footer starts expanded. Only meaningful
    /// when <see cref="FooterVisibility"/> is <see cref="RouteableTaskDialogFooterVisibility.Auto"/>.
    /// </summary>
    public bool IsFooterExpanded { get; set; }

    /// <summary>
    /// Gets whether the dialog shows a footer at all: there is a <see cref="Footer"/> and <see cref="FooterVisibility"/> is not <see cref="RouteableTaskDialogFooterVisibility.Never"/>.
    /// </summary>
    public bool ShowsFooter => Footer is not null && FooterVisibility != RouteableTaskDialogFooterVisibility.Never;

    /// <summary>
    /// Gets whether the footer can be opened and closed by the user (<see cref="RouteableTaskDialogFooterVisibility.Auto"/>).
    /// </summary>
    public bool IsFooterCollapsible => ShowsFooter && FooterVisibility == RouteableTaskDialogFooterVisibility.Auto;

    /// <summary>
    /// Gets whether the footer is open when the dialog appears: always for <see cref="RouteableTaskDialogFooterVisibility.Always"/>,
    /// <see cref="IsFooterExpanded"/> otherwise.
    /// </summary>
    public bool FooterStartsExpanded => FooterVisibility == RouteableTaskDialogFooterVisibility.Always || IsFooterExpanded;

    /// <summary>
    /// Gets or sets the footer content, shown/hidden per
    /// <see cref="FooterVisibility"/>. Typically used for supplementary
    /// details (e.g. error diagnostics) the user can optionally expand.
    /// </summary>
    public object? Footer { get; set; }

    /// <summary>
    /// Occurs before the dialog is opened
    /// </summary>
    public Action? Opening;

    /// <summary>
    /// Occurs after the dialog is opened.
    /// </summary>
    public Action? Opened;

    /// <summary>
    /// Occurs after the dialog starts to close, but before it is closed and before the Closed event occurs.
    /// </summary>
    /// <remarks>
    /// Return <see langword="true"/> to cancel the close and keep the
    /// dialog open; return <see langword="false"/> to allow it to proceed.
    /// Unlike the <c>Closing</c> callback of <see cref="RouteableContentDialog"/>,
    /// this one isn't told which result triggered the close.
    /// </remarks>
    public Func<bool>? Closing;

    /// <summary>
    /// Occurs after the dialog is closed.
    /// </summary>
    public Action? Closed;
}

/// <summary>
/// Controls whether and when a <see cref="RouteableTaskDialog.Footer"/> is shown.
/// </summary>
public enum RouteableTaskDialogFooterVisibility
{
    /// <summary>
    /// The footer is never shown
    /// </summary>
    Never,

    /// <summary>
    /// The footer is hidden by default, but can be expanded open
    /// </summary>
    Auto,

    /// <summary>
    /// The footer is always visible
    /// </summary>
    Always
}

/// <summary>
/// Defines constants that for standardized results from a <see cref="RouteableTaskDialogResult"/>
/// </summary>
public enum RouteableTaskDialogResult
{
    /// <summary>
    /// No button was tapped — e.g. the dialog was dismissed without a
    /// choice (Escape, tapping outside, or programmatic close).
    /// </summary>
    None,

    /// <summary>
    /// The "OK" button was tapped.
    /// </summary>
    OK,

    /// <summary>
    /// The "Cancel" button was tapped.
    /// </summary>
    Cancel,

    /// <summary>
    /// The "Yes" button was tapped.
    /// </summary>
    Yes,

    /// <summary>
    /// The "No" button was tapped.
    /// </summary>
    No,

    /// <summary>
    /// The "Retry" button was tapped.
    /// </summary>
    Retry,

    /// <summary>
    /// The "Close" button was tapped.
    /// </summary>
    Close
}