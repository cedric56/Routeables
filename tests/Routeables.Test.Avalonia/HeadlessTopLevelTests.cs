using Avalonia.Controls;
using Avalonia.Threading;
using Routeables.Avalonia;
using Xunit;

namespace Routeables.Test.Avalonia;

/// <summary>
/// <see cref="TopLevelStateManager"/> with real (headless) windows: the window that is shown or focused becomes the active top level that the
/// dialogs are opened over, the change is announced, and a window that closes is let go.
/// </summary>
[Collection("Avalonia headless")]
public sealed class HeadlessTopLevelTests(HeadlessApp app)
{
    /// <summary>Polls <paramref name="condition"/> on the UI thread until it becomes true or a 5-second timeout elapses.</summary>
    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition()) return;
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition was not met in 5 seconds.");
            await Task.Delay(10);
        }
    }

    /// <summary>Creates, shows and returns a temporary window for the tests.</summary>
    private static Window Shown()
    {
        var window = new Window { Width = 200, Height = 150, Content = new TextBlock { Text = "w" } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>A window that is shown becomes the active top level and raises <see cref="ITopLevelStateManager.ActiveChanged"/>.</summary>
    [Fact]
    public Task A_window_that_is_shown_becomes_the_active_top_level_and_the_change_is_announced() => app.RunAsync(async () =>
    {
        var manager = TopLevelStateManager.Default;
        var announced = new List<object?>();
        EventHandler handler = (sender, _) => announced.Add(sender);
        manager.ActiveChanged += handler;
        var window = Shown();
        try
        {
            await UntilAsync(() => ReferenceEquals(manager.GetActive(), window));

            Assert.Contains(window, announced);
        }
        finally
        {
            manager.ActiveChanged -= handler;
            window.Close();
        }
    });

    /// <summary>The window shown last is active; activating a previous window brings it back to the front.</summary>
    [Fact]
    public Task The_window_shown_last_is_the_active_one_and_the_first_comes_back_when_it_is_focused() => app.RunAsync(async () =>
    {
        var manager = TopLevelStateManager.Default;
        var first = Shown();
        var second = Shown();
        try
        {
            await UntilAsync(() => ReferenceEquals(manager.GetActive(), second));

            manager.OnActivated(first);

            Assert.Same(first, manager.GetActive());
        }
        finally
        {
            second.Close();
            first.Close();
        }
    });

    /// <summary>Activating a window that is already active does not raise <see cref="ITopLevelStateManager.ActiveChanged"/>.</summary>
    [Fact]
    public Task Activating_the_window_that_is_already_active_announces_nothing() => app.RunAsync(async () =>
    {
        var manager = TopLevelStateManager.Default;
        var window = Shown();
        try
        {
            await UntilAsync(() => ReferenceEquals(manager.GetActive(), window));
            var count = 0;
            EventHandler handler = (_, _) => count++;
            manager.ActiveChanged += handler;

            manager.OnActivated(window);
            manager.OnActivated(window);

            manager.ActiveChanged -= handler;
            Assert.Equal(0, count);
        }
        finally { window.Close(); }
    });

    /// <summary>Closing the active window releases it and raises <see cref="ITopLevelStateManager.ActiveChanged"/>.</summary>
    [Fact]
    public Task The_active_window_that_closes_is_let_go_and_the_change_is_announced() => app.RunAsync(async () =>
    {
        var manager = TopLevelStateManager.Default;
        var window = Shown();
        await UntilAsync(() => ReferenceEquals(manager.GetActive(), window));
        var count = 0;
        EventHandler handler = (_, _) => count++;
        manager.ActiveChanged += handler;

        window.Close();
        Dispatcher.UIThread.RunJobs();

        manager.ActiveChanged -= handler;
        Assert.NotSame(window, manager.GetActive());
        Assert.True(count >= 1);
    });

    /// <summary>Closing a non-active window leaves the active top level unchanged and does not raise the event.</summary>
    [Fact]
    public Task A_window_that_is_not_the_active_one_closes_without_changing_it() => app.RunAsync(async () =>
    {
        var manager = TopLevelStateManager.Default;
        var first = Shown();
        var second = Shown();
        try
        {
            await UntilAsync(() => ReferenceEquals(manager.GetActive(), second));
            var count = 0;
            EventHandler handler = (_, _) => count++;
            manager.ActiveChanged += handler;

            first.Close();
            Dispatcher.UIThread.RunJobs();

            manager.ActiveChanged -= handler;
            Assert.Same(second, manager.GetActive());
            Assert.Equal(0, count);
        }
        finally
        {
            second.Close();
            first.Close();
        }
    });
}