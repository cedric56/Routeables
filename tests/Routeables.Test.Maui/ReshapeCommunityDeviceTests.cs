using System.Collections.ObjectModel;
using System.Windows.Input;
using DeviceRunners.UITesting;
using Microsoft.Maui.Controls.Shapes;
using Reshapes.Maui.Community;
using Reshapes.Maui.CommunityToolkit;
using Routeables.Abstraction;
using Routeables.Maui.Services;
using Routeables.Services;
using Xunit;

namespace Routeables.Test.Maui;

/// <summary>The CommunityToolkit Reshape: what it does when no page can host a popup, what it refuses, and the look of its popups.</summary>
public class ReshapeCommunityDeviceTests
{
    private sealed class Context(Page? page = null) : IRouteableDialogWindowContext
    {
        public Page? CurrentPage { get; } = page;
    }

    private sealed class ClosingViewModel : ICloseableViewModel<int>
    {
        public string Title => "Modal";
        public ObservableCollection<RouteableCommand> Commands { get; } = [];
        public ICommand? CloseCommand => null;
        public event EventHandler<int>? CloseRequested { add { } remove { } }
        public Task CloseAsync(int value) => Task.CompletedTask;
    }

    private static ReshapeMessageBoxService WithoutPage() => new(new Context());

    // ------------------------------------------------------------------------------------------------ no page

    [UIFact]
    public async Task A_message_box_needs_a_page()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => WithoutPage().ShowMessageAsync("T", "M", "Ok"));

    [UIFact]
    public async Task A_content_dialog_needs_a_page()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => WithoutPage().ShowContentDialogAsync(new RouteableContentDialog { Title = "T" }));

    [UIFact]
    public async Task A_task_dialog_needs_a_page()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => WithoutPage().ShowTaskDialogAsync(new RouteableTaskDialog(), RouteableTaskDialogResult.OK));

    [UIFact]
    public async Task A_modal_needs_a_page()
        => await Assert.ThrowsAsync<InvalidOperationException>(
            () => WithoutPage().ShowModalAsync<ClosingViewModel, int>(new ClosingViewModel(), () => new Label()));

    // ------------------------------------------------------------------------------------------------ what it refuses

    [UIFact]
    public async Task A_content_dialog_and_a_task_dialog_without_parameters_are_refused()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => WithoutPage().ShowContentDialogAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => WithoutPage().ShowTaskDialogAsync(null!));
    }

    [UIFact]
    public async Task A_modal_without_a_view_model_or_a_view_factory_is_refused()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => WithoutPage().ShowModalAsync<ClosingViewModel, int>(null!, () => new Label()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => WithoutPage().ShowModalAsync<ClosingViewModel, int>(new ClosingViewModel(), null!));
    }

    // ------------------------------------------------------------------------------------------------ the popups

    [UIFact]
    public void The_popup_chrome_is_a_rounded_dialog_that_a_tap_outside_does_not_dismiss()
    {
        var options = PopupLook.Options();

        Assert.False(options.CanBeDismissedByTappingOutsideOfPopup);
        var shape = Assert.IsType<RoundRectangle>(options.Shape);
        Assert.Equal(new CornerRadius(RouteableNativeDialogLook.CornerRadius), shape.CornerRadius);
    }

    [UIFact]
    public void A_content_dialog_popup_has_a_session_and_a_dialog_for_content()
    {
        var popup = new ReshapeMessageBoxService.ContentDialogPopup(new RouteableContentDialog { Title = "T", CloseButtonText = "Close" });

        Assert.NotNull(popup.Session);
        Assert.NotNull(popup.Content);
        Assert.False(popup.CanBeDismissedByTappingOutsideOfPopup);
    }

    [UIFact]
    public void A_task_dialog_popup_has_a_session_and_a_dialog_for_content()
    {
        var popup = new ReshapeMessageBoxService.TaskDialogPopup(new RouteableTaskDialog(), [RouteableTaskDialogResult.OK]);

        Assert.NotNull(popup.Session);
        Assert.NotNull(popup.Content);
        Assert.False(popup.CanBeDismissedByTappingOutsideOfPopup);
    }

    // ------------------------------------------------------------------------------------------------ notifications

    [UIFact]
    public void A_notification_without_a_page_falls_back_and_never_breaks_the_caller()
    {
        var service = new ReshapeNotificationService(new Context());

        var exception = Record.Exception(() => service.Show("T", "M", RouteableNotificationType.Warning, TimeSpan.FromSeconds(1)));

        Assert.Null(exception);
    }
}
