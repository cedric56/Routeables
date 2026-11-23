using Routeables.AspNetCore;
using Routeables.Services;

namespace Routeables.Tests.Hosts;

/// <summary>A dialog of the web host that nobody answers (the session ends) is closed all the same: Closed follows Opening and Opened.</summary>
public class AspNetCoreDialogClosedTests
{
    [Fact]
    public async Task A_content_dialog_cancelled_with_the_session_runs_Closed_once()
    {
        var interactions = new RouteableInteractions();
        var service = new RouteableDialogService(interactions);
        var events = new List<string>();

        var shown = service.ShowContentDialogAsync(new RouteableContentDialog
        {
            Title = "T",
            CloseButtonText = "Close",
            Opening = () => events.Add("opening"),
            Opened = () => events.Add("opened"),
            Closed = () => events.Add("closed"),
        });
        interactions.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => shown);
        Assert.Equal(["opening", "opened", "closed"], events);
    }

    [Fact]
    public async Task A_task_dialog_cancelled_with_the_session_runs_Closed_once()
    {
        var interactions = new RouteableInteractions();
        var service = new RouteableDialogService(interactions);
        var events = new List<string>();

        var shown = service.ShowTaskDialogAsync(new RouteableTaskDialog
        {
            Title = "T",
            Opening = () => events.Add("opening"),
            Opened = () => events.Add("opened"),
            Closed = () => events.Add("closed"),
        });
        interactions.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => shown);
        Assert.Equal(["opening", "opened", "closed"], events);
    }
}
