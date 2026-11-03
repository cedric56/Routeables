using Routeables.Abstraction;
using Routeables.Razor;
using Routeables.Razor.Services;
using Routeables.Services;

namespace Routeables.Tests.Razor;

/// <summary>A circuit that ends with dialogs open must not leave the code awaiting them suspended for ever.</summary>
public class DialogServicesDisposeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Disposing_the_message_queue_completes_the_waiting_dialogs()
    {
        var service = new RouteableMessageQueue();
        service.Changed += () => { };      // a host is displayed
        var dialogs = new RouteableDialogService(new DefaultMessageBoxService(service, new RouteableContentDialogQueue(), new RouteableTaskDialogQueue()));
        var shown = dialogs.ShowErrorAsync(new InvalidOperationException("first"));
        var queued = dialogs.ShowErrorAsync(new InvalidOperationException("second"));

        service.Dispose();

        await shown.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        await queued.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Assert.Null(service.Current);
    }

    [Fact]
    public async Task Disposing_the_task_dialog_queue_completes_the_waiting_dialogs_with_None()
    {
        var service = new RouteableTaskDialogQueue();
        service.Changed += () => { };
        var shown = service.ShowAsync(new RouteableTaskDialog { Title = "First" });
        var queued = service.ShowAsync(new RouteableTaskDialog { Title = "Second" });

        service.Dispose();

        Assert.Equal(RouteableTaskDialogResult.None, await shown.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.Equal(RouteableTaskDialogResult.None, await queued.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.Null(service.Current);
    }
}
