using Routeables.Razor.Services;
using Routeables.Services;

namespace Routeables.Tests.Razor;

/// <summary>
/// The queues that the dialog hosts of Razor draw from: one dialog at a time in the order it was asked for, no dialog without a host,
/// a dialog that is closed once, a veto, and a circuit that ends with dialogs still open.
/// </summary>
public class DialogQueueTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RouteableMessageQueue Hosted(out Func<int> changes)
    {
        var queue = new RouteableMessageQueue();
        var count = 0;
        queue.Changed += () => count++;
        changes = () => count;
        return queue;
    }

    private static Task<int> Show(RouteableMessageQueue queue, string title, int dismiss = -1)
        => queue.ShowAsync(title, "message", dismiss, new DialogButton("Yes", 1, IsPrimary: true), new DialogButton("No", 0));

    // ------------------------------------------------------------------------------------------------ message boxes

    [Fact]
    public async Task A_dialog_without_a_host_is_refused_with_a_message_that_names_the_host()
    {
        var queue = new RouteableMessageQueue();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Show(queue, "x"));

        Assert.Contains("RouteableDialogHost", ex.Message);
        Assert.Contains("dialogs", ex.Message);
        Assert.Null(queue.Current);
    }

    [Fact]
    public void The_first_dialog_is_shown_at_once_and_the_host_is_told()
    {
        var queue = Hosted(out var changes);

        _ = Show(queue, "first");

        Assert.Equal("first", queue.Current!.Title);
        Assert.Equal("message", queue.Current.Message);
        Assert.Equal(2, queue.Current.Buttons.Length);
        Assert.Equal(1, changes());
    }

    [Fact]
    public async Task The_dialogs_are_shown_one_at_a_time_in_the_order_they_were_asked_for()
    {
        var queue = Hosted(out _);
        var first = Show(queue, "first");
        var second = Show(queue, "second");
        var third = Show(queue, "third");

        Assert.Equal("first", queue.Current!.Title);
        Assert.False(second.IsCompleted);

        queue.Complete(1);
        Assert.Equal(1, await first.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.Equal("second", queue.Current!.Title);

        queue.Complete(0);
        Assert.Equal(0, await second.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.Equal("third", queue.Current!.Title);

        queue.Complete(7);
        Assert.Equal(7, await third.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.Null(queue.Current);
    }

    [Fact]
    public void Completing_with_no_dialog_does_nothing_and_does_not_tell_the_host()
    {
        var queue = Hosted(out var changes);

        queue.Complete(1);

        Assert.Null(queue.Current);
        Assert.Equal(0, changes());
    }

    [Fact]
    public void The_host_is_told_when_a_dialog_comes_and_when_it_goes()
    {
        var queue = Hosted(out var changes);

        _ = Show(queue, "first");
        _ = Show(queue, "second");
        queue.Complete(1);
        queue.Complete(1);

        Assert.Equal(4, changes());
    }

    [Fact]
    public async Task A_dialog_that_is_not_the_current_one_cannot_be_finished()
    {
        var queue = Hosted(out _);
        var first = Show(queue, "first");
        _ = Show(queue, "second");
        var current = queue.Current!;
        queue.Complete(1);
        await first.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        // The first one is already closed: a second click on its button changes nothing.
        queue.Finish(current, 99);

        Assert.Equal("second", queue.Current!.Title);
    }

    [Fact]
    public async Task A_dialog_finished_twice_completes_once()
    {
        var queue = Hosted(out var changes);
        var shown = Show(queue, "only");
        var request = queue.Current!;

        queue.Finish(request, 1);
        var afterFirst = changes();
        queue.Finish(request, 2);

        Assert.Equal(1, await shown.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.Equal(afterFirst, changes());
    }

    [Fact]
    public async Task The_closed_callback_runs_before_the_dialog_is_completed()
    {
        var queue = Hosted(out _);
        var order = new List<string>();
        var shown = Show(queue, "only");
        _ = shown.ContinueWith(_ => order.Add("completed"), TaskScheduler.Default);

        queue.Finish(queue.Current!, 1, () => order.Add("closed"));
        await shown.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        await Task.Delay(50, Ct);

        Assert.Equal(["closed", "completed"], order);
    }

    [Fact]
    public async Task A_closed_callback_that_throws_does_not_leave_the_dialog_hanging()
    {
        var queue = Hosted(out _);
        var shown = Show(queue, "first");
        var next = Show(queue, "second");

        queue.Finish(queue.Current!, 5, () => throw new InvalidOperationException("boom"));

        Assert.Equal(5, await shown.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.Equal("second", queue.Current!.Title);
        Assert.False(next.IsCompleted);
    }

    [Fact]
    public async Task Disposing_gives_every_waiting_dialog_its_dismiss_result_and_empties_the_queue()
    {
        var queue = Hosted(out _);
        var first = Show(queue, "first", dismiss: -1);
        var second = Show(queue, "second", dismiss: -2);

        queue.Dispose();

        Assert.Equal(-1, await first.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.Equal(-2, await second.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.Null(queue.Current);
    }

    [Fact]
    public void Disposing_stops_telling_the_host()
    {
        var queue = Hosted(out var changes);
        _ = Show(queue, "first");
        var before = changes();

        queue.Dispose();
        queue.Complete(1);

        Assert.Equal(before, changes());
    }

    [Fact]
    public async Task Disposing_twice_is_harmless()
    {
        var queue = Hosted(out _);
        var shown = Show(queue, "first");

        queue.Dispose();
        queue.Dispose();

        Assert.Equal(-1, await shown.WaitAsync(TimeSpan.FromSeconds(5), Ct));
    }

    [Fact]
    public async Task A_dialog_asked_for_after_disposing_is_refused_because_no_host_is_listening()
    {
        var queue = Hosted(out _);
        queue.Dispose();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Show(queue, "late"));
    }

    [Fact]
    public async Task Dialogs_asked_for_from_many_threads_are_all_shown_once_each()
    {
        var queue = Hosted(out _);
        var started = new System.Collections.Concurrent.ConcurrentBag<Task<int>>();
        await Task.WhenAll(Enumerable.Range(0, 40).Select(i => Task.Run(() => started.Add(Show(queue, $"d{i}", dismiss: i)), Ct)));
        var shown = 0;

        while (queue.Current is not null)
        {
            shown++;
            queue.Complete(1);
        }

        Assert.Equal(40, shown);
        Assert.Equal(40, started.Count);
        Assert.All(started, t => Assert.Equal(1, t.Result));
    }

    // ------------------------------------------------------------------------------------------------ content dialogs

    private static RouteableContentDialogQueue HostedContent()
    {
        var queue = new RouteableContentDialogQueue();
        queue.Changed += () => { };
        return queue;
    }

    [Fact]
    public async Task A_content_dialog_without_a_host_is_refused_with_a_message_that_names_the_host()
    {
        var queue = new RouteableContentDialogQueue();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => queue.ShowAsync(new RouteableContentDialog { Title = "x" }));

        Assert.Contains("RouteableContentDialogHost", ex.Message);
    }

    [Fact]
    public async Task A_content_dialog_refuses_no_parameters()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => HostedContent().ShowAsync(null!));

    [Fact]
    public async Task Dismissing_closes_the_content_dialog_with_None_and_tells_Closed_once()
    {
        var queue = HostedContent();
        var closed = 0;
        var shown = queue.ShowAsync(new RouteableContentDialog { Title = "x", Closed = () => closed++ });

        queue.Dismiss();
        queue.Dismiss();

        Assert.Equal(RouteableContentDialogResult.None, await shown.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.Equal(1, closed);
        Assert.Null(queue.Current);
    }

    [Fact]
    public async Task A_veto_keeps_the_content_dialog_open_until_it_is_lifted()
    {
        var queue = HostedContent();
        var veto = true;
        var asked = new List<string>();
        var shown = queue.ShowAsync(new RouteableContentDialog { Title = "x", Closing = result => { asked.Add(result); return veto; } });

        queue.Dismiss();
        Assert.NotNull(queue.Current);
        Assert.False(shown.IsCompleted);

        veto = false;
        queue.Dismiss();

        Assert.Equal(RouteableContentDialogResult.None, await shown.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.Equal(["None", "None"], asked);
    }

    [Fact]
    public async Task A_closing_callback_that_throws_does_not_keep_the_content_dialog_open()
    {
        var queue = HostedContent();
        var shown = queue.ShowAsync(new RouteableContentDialog { Title = "x", Closing = _ => throw new InvalidOperationException("boom") });

        queue.Dismiss();

        Assert.Equal(RouteableContentDialogResult.None, await shown.WaitAsync(TimeSpan.FromSeconds(5), Ct));
    }

    [Fact]
    public async Task The_caller_can_close_the_content_dialog_from_code_and_no_longer_once_it_is_closed()
    {
        var queue = HostedContent();
        var dialog = new RouteableContentDialog { Title = "x" };
        var shown = queue.ShowAsync(dialog);
        Assert.NotNull(dialog.Close);

        dialog.Close!();

        Assert.Equal(RouteableContentDialogResult.None, await shown.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.Null(dialog.Close);
    }

    [Fact]
    public async Task Content_dialogs_follow_each_other_and_dispose_gives_None_to_the_ones_waiting()
    {
        var queue = HostedContent();
        var first = queue.ShowAsync(new RouteableContentDialog { Title = "first" });
        var second = queue.ShowAsync(new RouteableContentDialog { Title = "second" });
        Assert.Equal("first", queue.Current!.Parameters.Title);

        queue.Dismiss();
        await first.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Assert.Equal("second", queue.Current!.Parameters.Title);

        queue.Dispose();
        Assert.Equal(RouteableContentDialogResult.None, await second.WaitAsync(TimeSpan.FromSeconds(5), Ct));
    }

    [Fact]
    public void Dismissing_with_no_content_dialog_does_nothing()
        => HostedContent().Dismiss();

    // ------------------------------------------------------------------------------------------------ task dialogs

    private static RouteableTaskDialogQueue HostedTask()
    {
        var queue = new RouteableTaskDialogQueue();
        queue.Changed += () => { };
        return queue;
    }

    [Fact]
    public async Task A_task_dialog_without_a_host_is_refused_with_a_message_that_names_the_host()
    {
        var queue = new RouteableTaskDialogQueue();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => queue.ShowAsync(new RouteableTaskDialog { Title = "x" }));

        Assert.Contains("RouteableTaskDialogHost", ex.Message);
    }

    [Fact]
    public async Task A_task_dialog_refuses_no_parameters()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => HostedTask().ShowAsync(null!));

    [Fact]
    public async Task Dismissing_closes_the_task_dialog_with_None_and_tells_Closed_once()
    {
        var queue = HostedTask();
        var closed = 0;
        var shown = queue.ShowAsync(new RouteableTaskDialog { Title = "x", Closed = () => closed++ });

        queue.Dismiss();
        queue.Dismiss();

        Assert.Equal(RouteableTaskDialogResult.None, await shown.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.Equal(1, closed);
    }

    [Fact]
    public async Task A_veto_keeps_the_task_dialog_open_until_it_is_lifted()
    {
        var queue = HostedTask();
        var veto = true;
        var shown = queue.ShowAsync(new RouteableTaskDialog { Title = "x", Closing = () => veto });

        queue.Dismiss();
        Assert.NotNull(queue.Current);

        veto = false;
        queue.Dismiss();

        Assert.Equal(RouteableTaskDialogResult.None, await shown.WaitAsync(TimeSpan.FromSeconds(5), Ct));
    }

    [Fact]
    public async Task Task_dialogs_follow_each_other_and_dispose_gives_None_to_the_ones_waiting()
    {
        var queue = HostedTask();
        var first = queue.ShowAsync(new RouteableTaskDialog { Title = "first" });
        var second = queue.ShowAsync(new RouteableTaskDialog { Title = "second" });

        queue.Dismiss();
        await first.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Assert.Equal("second", queue.Current!.Parameters.Title);

        queue.Dispose();
        Assert.Equal(RouteableTaskDialogResult.None, await second.WaitAsync(TimeSpan.FromSeconds(5), Ct));
    }

    [Fact]
    public void Dismissing_with_no_task_dialog_does_nothing()
        => HostedTask().Dismiss();
}
