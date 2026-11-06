using Microsoft.Extensions.DependencyInjection;
using Routeables.AspNetCore;
using Routeables.Core;
using Routeables.Services;

namespace Routeables.Tests.Hosts;

/// <summary>What a web page shares with the view models that ask it something: the dialogs, the notifications and the run of an action.</summary>
public class AspNetCoreInteractionsTests
{
    private static async Task<bool> EventuallyAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
            await Task.Delay(10, TestContext.Current.CancellationToken);
        return condition();
    }

    // ---------------------------------------------------------------- RouteableInteractions: dialogs

    [Fact]
    public async Task A_dialog_waits_until_it_is_answered_and_then_leaves_the_page()
    {
        using var interactions = new RouteableInteractions();
        var dialog = new RouteableDialog("Title", null, "Body", ["Yes", "No"]);

        var answer = interactions.AskAsync(dialog);

        Assert.Same(dialog, interactions.Pending);
        Assert.False(answer.IsCompleted);

        await interactions.AnswerAsync(1);

        Assert.Equal(1, await answer);
        Assert.Null(interactions.Pending);
    }

    [Fact]
    public async Task Dialogs_are_asked_one_after_the_other()
    {
        using var interactions = new RouteableInteractions();
        var first = new RouteableDialog("First", null, null, ["Ok"]);
        var second = new RouteableDialog("Second", null, null, ["Ok"]);
        var a = interactions.AskAsync(first);
        var b = interactions.AskAsync(second);

        Assert.Same(first, interactions.Pending);
        await interactions.AnswerAsync(0);
        Assert.Equal(0, await a);
        Assert.Same(second, interactions.Pending);
        await interactions.AnswerAsync(0);
        Assert.Equal(0, await b);
        Assert.Null(interactions.Pending);
    }

    [Fact]
    public async Task Answering_when_nothing_is_asked_does_nothing()
    {
        using var interactions = new RouteableInteractions();

        await interactions.AnswerAsync(0);

        Assert.Null(interactions.Pending);
    }

    [Fact]
    public async Task A_dialog_nobody_answers_is_cancelled_with_the_session()
    {
        var interactions = new RouteableInteractions();
        var answer = interactions.AskAsync(new RouteableDialog("T", null, null, ["Ok"]));

        interactions.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => answer);
        Assert.Null(interactions.Pending);
    }

    // ---------------------------------------------------------------- RouteableInteractions: running an action

    [Fact]
    public async Task An_action_that_asks_a_dialog_returns_when_it_asks_and_goes_on_with_the_answer()
    {
        using var interactions = new RouteableInteractions();
        var result = string.Empty;

        // The request that starts the action returns once the dialog is on the page...
        await interactions.StartAsync(async () =>
        {
            var button = await interactions.AskAsync(new RouteableDialog("T", null, null, ["Yes", "No"]));
            result = "button " + button;
        });
        Assert.NotNull(interactions.Pending);
        Assert.Equal(string.Empty, result);

        // ...and the request that answers returns when the action ends.
        await interactions.AnswerAsync(1);
        Assert.Equal("button 1", result);
    }

    [Fact]
    public async Task An_action_that_does_not_ask_ends_inside_the_request()
    {
        using var interactions = new RouteableInteractions();
        var done = false;

        await interactions.StartAsync(() => { done = true; return Task.CompletedTask; });

        Assert.True(done);
    }

    [Fact]
    public async Task The_failure_of_an_action_reaches_the_request()
    {
        using var interactions = new RouteableInteractions();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => interactions.StartAsync(() => throw new InvalidOperationException("boom")));
    }

    [Fact]
    public async Task The_failure_of_an_action_after_a_dialog_reaches_the_request_that_answered()
    {
        using var interactions = new RouteableInteractions();
        await interactions.StartAsync(async () =>
        {
            await interactions.AskAsync(new RouteableDialog("T", null, null, ["Ok"]));
            throw new InvalidOperationException("after the answer");
        });

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => interactions.AnswerAsync(0));

        Assert.Equal("after the answer", failure.Message);
    }

    // ---------------------------------------------------------------- RouteableInteractions: notifications

    [Fact]
    public void A_notification_stays_until_it_is_closed_and_tells_what_was_done_with_it()
    {
        using var interactions = new RouteableInteractions();
        var told = new List<string>();
        interactions.Notify("Saved", "It is saved", RouteableNotificationType.Success, null, () => told.Add("click"), () => told.Add("close"));

        var toast = Assert.Single(interactions.Toasts);
        Assert.Equal("Saved", toast.Title);
        Assert.Equal("It is saved", toast.Message);
        Assert.Equal(RouteableNotificationType.Success, toast.Type);

        interactions.Dismiss(toast.Id, click: true);

        Assert.Empty(interactions.Toasts);
        Assert.Equal(2, told.Count);
        Assert.Equal("click", told[0]);   // a click is a close too
        Assert.Equal("close", told[1]);
    }

    [Fact]
    public void Closing_without_a_click_does_not_click()
    {
        using var interactions = new RouteableInteractions();
        var told = new List<string>();
        interactions.Notify("T", "M", RouteableNotificationType.Information, null, () => told.Add("click"), () => told.Add("close"));

        interactions.Dismiss(interactions.Toasts[0].Id, click: false);
        interactions.Dismiss(interactions.Toasts.Count + 99, click: true);   // an unknown one is ignored

        Assert.Single(told);
        Assert.Equal("close", told[0]);
    }

    [Fact]
    public void Each_notification_has_its_own_number()
    {
        using var interactions = new RouteableInteractions();
        interactions.Notify("A", "a", RouteableNotificationType.Warning, null, null, null);
        interactions.Notify("B", "b", RouteableNotificationType.Error, null, null, null);

        Assert.NotEqual(interactions.Toasts[0].Id, interactions.Toasts[1].Id);
    }

    [Fact]
    public async Task A_notification_whose_time_is_over_is_dropped_by_Expire_and_the_others_stay()
    {
        using var interactions = new RouteableInteractions();
        var closed = 0;
        interactions.Notify("Short", "s", RouteableNotificationType.Information, TimeSpan.FromMilliseconds(1), null, () => closed++);
        interactions.Notify("Long", "l", RouteableNotificationType.Information, TimeSpan.FromHours(1), null, null);
        interactions.Notify("Forever", "f", RouteableNotificationType.Information, null, null, null);

        await Task.Delay(30, TestContext.Current.CancellationToken);
        interactions.Expire();

        Assert.Equal(2, interactions.Toasts.Count);
        Assert.DoesNotContain(interactions.Toasts, t => t.Title == "Short");
        Assert.Equal(1, closed);
    }

    // ---------------------------------------------------------------- the services

    private static (RouteableDialogService Service, RouteableInteractions Interactions) Dialogs()
    {
        var interactions = new RouteableInteractions();
        return (new RouteableDialogService(interactions), interactions);
    }

    [Fact]
    public async Task A_message_has_the_buttons_of_its_text_and_returns_the_one_that_was_chosen()
    {
        var (service, interactions) = Dialogs();
        using var _ = interactions;

        var yes = service.ShowYesNoAsync("Sure?", "Ask");
        var dialog = interactions.Pending!;

        Assert.Equal("Ask", dialog.Title);
        Assert.Equal("Sure?", dialog.Body);
        Assert.Equal(2, dialog.Buttons.Count);
        Assert.Equal("Yes", dialog.Buttons[0]);
        Assert.Equal("No", dialog.Buttons[1]);

        await interactions.AnswerAsync(0);
        Assert.True(await yes);

        var no = service.ShowYesNoAsync("Again?", "Ask");
        await interactions.AnswerAsync(1);
        Assert.False(await no);
    }

    [Fact]
    public async Task A_content_dialog_offers_its_buttons_in_the_order_of_the_other_hosts_and_runs_what_was_chosen()
    {
        var (service, interactions) = Dialogs();
        using var _ = interactions;
        var events = new List<string>();
        var parameters = new RouteableContentDialog
        {
            Title = "Share",
            Content = "Send it?",
            PrimaryButtonText = "Send",
            IsPrimaryButtonEnabled = true,
            SecondaryButtonText = "Copy",
            IsSecondaryButtonEnabled = true,
            CloseButtonText = "Cancel",
            PrimaryButtonClick = () => events.Add("primary"),
            SecondaryButtonClick = () => events.Add("secondary"),
            CloseButtonClick = () => events.Add("close"),
            Opening = () => events.Add("opening"),
            Opened = () => events.Add("opened"),
            Closed = () => events.Add("closed")
        };

        var result = service.ShowContentDialogAsync(parameters);
        var dialog = interactions.Pending!;

        Assert.Equal("Share", dialog.Title);
        Assert.Equal("Send it?", dialog.Body);
        Assert.Equal(3, dialog.Buttons.Count);
        Assert.Equal("Send", dialog.Buttons[0]);
        Assert.Equal("Copy", dialog.Buttons[1]);
        Assert.Equal("Cancel", dialog.Buttons[2]);

        await interactions.AnswerAsync(1);

        Assert.Equal(RouteableContentDialogResult.Secondary, await result);
        Assert.Equal(4, events.Count);
        Assert.Equal("opening", events[0]);
        Assert.Equal("opened", events[1]);
        Assert.Equal("secondary", events[2]);
        Assert.Equal("closed", events[3]);
    }

    [Fact]
    public async Task A_content_dialog_without_a_close_text_is_closed_by_Close()
    {
        var (service, interactions) = Dialogs();
        using var _ = interactions;

        var result = service.ShowContentDialogAsync(new RouteableContentDialog { Title = "T", Content = "C", PrimaryButtonText = "Ok", IsPrimaryButtonEnabled = true });

        Assert.Equal(2, interactions.Pending!.Buttons.Count);
        Assert.Equal("Close", interactions.Pending.Buttons[1]);

        await interactions.AnswerAsync(1);
        Assert.Equal(RouteableContentDialogResult.None, await result);
    }

    [Fact]
    public async Task A_task_dialog_shows_its_header_footer_and_progress_and_returns_the_chosen_result()
    {
        var (service, interactions) = Dialogs();
        using var _ = interactions;

        var result = service.ShowTaskDialogAsync(
            new RouteableTaskDialog { Title = "Photos", Header = "Download?", Content = "All of them", Footer = "Later in the settings", FooterVisibility = RouteableTaskDialogFooterVisibility.Auto, ShowProgressBar = true },
            RouteableTaskDialogResult.Yes, RouteableTaskDialogResult.No, RouteableTaskDialogResult.Cancel);
        var dialog = interactions.Pending!;

        Assert.Equal("Photos", dialog.Title);
        Assert.Equal("Download?", dialog.Header);
        Assert.Equal("All of them", dialog.Body);
        Assert.Equal("Later in the settings", dialog.Footer);
        Assert.True(dialog.ShowProgress);
        Assert.Equal(3, dialog.Buttons.Count);
        Assert.Equal("Cancel", dialog.Buttons[2]);

        await interactions.AnswerAsync(2);
        Assert.Equal(RouteableTaskDialogResult.Cancel, await result);
    }

    [Fact]
    public async Task A_task_dialog_without_buttons_has_Ok_and_hides_a_footer_it_does_not_show()
    {
        var (service, interactions) = Dialogs();
        using var _ = interactions;

        var result = service.ShowTaskDialogAsync(new RouteableTaskDialog { Title = "T", Content = "C", Footer = "hidden", FooterVisibility = RouteableTaskDialogFooterVisibility.Never });
        var dialog = interactions.Pending!;

        Assert.Single(dialog.Buttons);
        Assert.Equal("OK", dialog.Buttons[0]);
        Assert.Null(dialog.Footer);
        Assert.False(dialog.ShowProgress);

        await interactions.AnswerAsync(0);
        Assert.Equal(RouteableTaskDialogResult.OK, await result);
    }

    [Fact]
    public void The_notification_service_puts_the_notification_on_the_page()
    {
        using var interactions = new RouteableInteractions();
        IRouteableNotificationService service = new RouteableNotificationService(interactions);

        service.Show("Tip", "Hello", RouteableNotificationType.Warning, TimeSpan.FromSeconds(3));

        var toast = Assert.Single(interactions.Toasts);
        Assert.Equal("Tip", toast.Title);
        Assert.Equal(RouteableNotificationType.Warning, toast.Type);
        Assert.Equal(TimeSpan.FromSeconds(3), toast.Expiration);
    }

    [Fact]
    public async Task The_service_of_the_visitor_is_the_one_the_view_models_get()
    {
        var services = new ServiceCollection();
        services.AddRouteablesAspNetCore();
        await using var provider = services.BuildServiceProvider();

        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();

        // A scope is a visitor: what one is asked is not asked to the other.
        Assert.Same(first.ServiceProvider.GetRequiredService<RouteableInteractions>(), first.ServiceProvider.GetRequiredService<RouteableInteractions>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<RouteableInteractions>(), second.ServiceProvider.GetRequiredService<RouteableInteractions>());
        Assert.IsType<RouteableDialogService>(first.ServiceProvider.GetRequiredService<IRouteableDialogService>());
        Assert.IsType<RouteableNotificationService>(first.ServiceProvider.GetRequiredService<IRouteableNotificationService>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<RouteablePageState>(), second.ServiceProvider.GetRequiredService<RouteablePageState>());

        var dialogs = first.ServiceProvider.GetRequiredService<IRouteableDialogService>();
        var ask = dialogs.ShowYesNoAsync("Sure?", "Ask");
        Assert.NotNull(first.ServiceProvider.GetRequiredService<RouteableInteractions>().Pending);
        Assert.Null(second.ServiceProvider.GetRequiredService<RouteableInteractions>().Pending);
        await first.ServiceProvider.GetRequiredService<RouteableInteractions>().AnswerAsync(0);
        Assert.True(await ask);
        Assert.True(await EventuallyAsync(() => ask.IsCompleted));
    }
}
