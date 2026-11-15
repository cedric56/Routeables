using Routeables.Services;

namespace Routeables.Tests.Routing;

/// <summary>What a message means for the hosts that draw their dialogs: the index of the button pressed.</summary>
public class RouteableMessageBoxDialogServiceTests
{
    private sealed class Host : RouteableMessageBoxDialogService
    {
        public string? Pressed { get; set; }
        public int ThreadHops { get; private set; }
        public (string Title, string Message, string Buttons)? Shown { get; private set; }

        protected override Task<T> RunOnInterfaceThreadAsync<T>(Func<Task<T>> action) { ThreadHops++; return action(); }

        protected override Task<string?> ShowMessageBoxAsync(string title, string message, string buttonText)
        {
            Shown = (title, message, buttonText);
            return Task.FromResult(Pressed);
        }

        protected override Task<RouteableContentDialogResult> ShowContentAsync(RouteableContentDialog @params)
            => Task.FromResult(default(RouteableContentDialogResult)!);

        protected override Task<RouteableTaskDialogResult> ShowTaskAsync(RouteableTaskDialog @params, RouteableTaskDialogResult[] results)
            => Task.FromResult(RouteableTaskDialogResult.None);
    }

    [Theory]
    [InlineData("YesNo", "Yes", 0)]
    [InlineData("YesNo", "No", 1)]
    [InlineData("YesNoCancel", "Cancel", 2)]
    public async Task The_index_is_the_position_of_the_pressed_button(string buttons, string pressed, int expected)
    {
        var host = new Host { Pressed = pressed };

        var index = await host.ShowMessageAsync("Sure?", "Title", buttons);

        Assert.Equal(expected, index);
        Assert.Equal(1, host.ThreadHops);
        Assert.Equal(("Title", "Sure?", buttons), host.Shown);
    }

    [Fact]
    public async Task A_dismissed_dialog_gives_minus_one()
    {
        Assert.Equal(-1, await new Host { Pressed = null }.ShowMessageAsync("m", "t", "YesNo"));
        Assert.Equal(-1, await new Host { Pressed = "Unknown" }.ShowMessageAsync("m", "t", "YesNo"));
    }

    [Fact]
    public async Task A_task_dialog_asked_with_nothing_is_none_and_does_not_reach_the_host()
    {
        var host = new Host();
        Assert.Equal(RouteableTaskDialogResult.None, await host.ShowTaskDialogAsync(null!));
        Assert.Equal(0, host.ThreadHops);
    }

    [Fact]
    public async Task A_content_dialog_asked_with_nothing_is_refused()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => new Host().ShowContentDialogAsync(null!));
}
