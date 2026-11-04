using Microsoft.Maui.Controls.Shapes;
using Reshapes.Maui.Community;
using Reshapes.Maui.CommunityToolkit;
using Routeables.Abstraction;
using Routeables.Maui.Services;
using Routeables.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Xunit;

namespace Routeables.Test.Maui;

/// <summary>The CommunityToolkit Reshape: what it does when no page can host a popup, what it refuses, and the look of its popups.</summary>
public class ReshapeCommunityDeviceTests
{
    /// <summary>A window context that answers with the page it was given, or no page at all.</summary>
    private sealed class Context(Page? page = null) : IRouteableDialogWindowContext
    {
        /// <summary>The page the popups would be hosted in: null to test the refusals.</summary>
        public Page? CurrentPage { get; } = page;
    }

    /// <summary>A view model that closes with an integer: enough to open a modal, it never raises its event.</summary>
    private sealed class ClosingViewModel : ICloseableViewModel<int>
    {
        /// <summary>The title every modal of the fixture answers with.</summary>
        public string Title => "Modal";

        /// <summary>No command: the tests do not press anything.</summary>
        public ObservableCollection<RouteableCommand> Commands { get; } = [];

        /// <summary>No close command either.</summary>
        public ICommand? CloseCommand => null;

        /// <summary>Nobody subscribes to it: the event of the interface is satisfied, not used.</summary>
        public event EventHandler<int>? CloseRequested { add { } remove { } }

        /// <summary>Closing at once: the tests of the fixture never reach a real dialog.</summary>
        public Task CloseAsync(int value) => Task.CompletedTask;
    }

    /// <summary>A message box service over a context that has no page: every show must be refused.</summary>
    private static ReshapeMessageBoxService WithoutPage() => new(new Context());

    // ------------------------------------------------------------------------------------------------ no page

    /// <summary>A message box asked without a page is refused, not silently ignored.</summary>
    [UIFact]
    public async Task A_message_box_needs_a_page()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => WithoutPage().ShowMessageAsync("T", "M", "Ok"));

    /// <summary>A content dialog asked without a page is refused too.</summary>
    [UIFact]
    public async Task A_content_dialog_needs_a_page()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => WithoutPage().ShowContentDialogAsync(new RouteableContentDialog { Title = "T" }));

    /// <summary>A task dialog asked without a page is refused as well.</summary>
    [UIFact]
    public async Task A_task_dialog_needs_a_page()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => WithoutPage().ShowTaskDialogAsync(new RouteableTaskDialog(), RouteableTaskDialogResult.OK));

    /// <summary>A modal asked without a page is refused like the other popups.</summary>
    [UIFact]
    public async Task A_modal_needs_a_page()
        => await Assert.ThrowsAsync<InvalidOperationException>(
            () => WithoutPage().ShowModalAsync<ClosingViewModel, int>(new ClosingViewModel(), () => new Label()));

    // ------------------------------------------------------------------------------------------------ what it refuses

    /// <summary>A content dialog or a task dialog without its parameters is refused before the page is even looked at.</summary>
    [UIFact]
    public async Task A_content_dialog_and_a_task_dialog_without_parameters_are_refused()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => WithoutPage().ShowContentDialogAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => WithoutPage().ShowTaskDialogAsync(null!));
    }

    /// <summary>A modal without a view model or without a view factory is refused.</summary>
    [UIFact]
    public async Task A_modal_without_a_view_model_or_a_view_factory_is_refused()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => WithoutPage().ShowModalAsync<ClosingViewModel, int>(null!, () => new Label()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => WithoutPage().ShowModalAsync<ClosingViewModel, int>(new ClosingViewModel(), null!));
    }

    // ------------------------------------------------------------------------------------------------ the popups

    /// <summary>The popups of the Reshape look like dialogs: rounded corners, and a tap outside does not dismiss them.</summary>
    [UIFact]
    public void The_popup_chrome_is_a_rounded_dialog_that_a_tap_outside_does_not_dismiss()
    {
        var options = PopupLook.Options();

        Assert.False(options.CanBeDismissedByTappingOutsideOfPopup);
        var shape = Assert.IsType<RoundRectangle>(options.Shape);
        Assert.Equal(new CornerRadius(RouteableNativeDialogLook.CornerRadius), shape.CornerRadius);
    }

    /// <summary>A content dialog popup carries a session and shows the dialog itself as its content.</summary>
    [UIFact]
    public void A_content_dialog_popup_has_a_session_and_a_dialog_for_content()
    {
        var popup = new ReshapeMessageBoxService.ContentDialogPopup(new RouteableContentDialog { Title = "T", CloseButtonText = "Close" });

        Assert.NotNull(popup.Session);
        Assert.NotNull(popup.Content);
        Assert.False(popup.CanBeDismissedByTappingOutsideOfPopup);
    }

    /// <summary>A task dialog popup carries a session and shows the dialog itself as its content.</summary>
    [UIFact]
    public void A_task_dialog_popup_has_a_session_and_a_dialog_for_content()
    {
        var popup = new ReshapeMessageBoxService.TaskDialogPopup(new RouteableTaskDialog(), [RouteableTaskDialogResult.OK]);

        Assert.NotNull(popup.Session);
        Assert.NotNull(popup.Content);
        Assert.False(popup.CanBeDismissedByTappingOutsideOfPopup);
    }

    // ------------------------------------------------------------------------------------------------ notifications

    /// <summary>A notification without a page falls back silently: showing a notification never throws at the caller.</summary>
    [UIFact]
    public void A_notification_without_a_page_falls_back_and_never_breaks_the_caller()
    {
        var service = new ReshapeNotificationService(new Context());

        var exception = Record.Exception(() => service.Show("T", "M", RouteableNotificationType.Warning, TimeSpan.FromSeconds(1)));

        Assert.Null(exception);
    }
}