using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Razor;
using Routeables.Razor.Services;
using Routeables.Services;

namespace Routeables.Tests.Razor;

public class TaskDialogServiceTests
{
    private static Task<RazorHostHarness<RouteableTaskDialogHost>> StartAsync() =>
        RazorHostHarness<RouteableTaskDialogHost>.StartAsync(services =>
        {
            services.AddScoped<RouteableTaskDialogService>();
            services.AddScoped<IRouteableTaskDialogService>(sp => sp.GetRequiredService<RouteableTaskDialogService>());
        });

    // Presses the button with this text on the dialog being shown.
    private static bool Press(RouteableTaskDialogService service, string text) =>
        service.Current!.Session.Buttons.Single(b => b.Text == text).Press();

    [Fact]
    public async Task The_host_renders_the_texts_the_progress_bar_and_one_button_per_result()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableTaskDialogService>();

        var task = ((IRouteableTaskDialogService)service).ShowAsync(
            new RouteableTaskDialog { Title = "Working", Header = "Saving <now>", SubHeader = "please wait", ShowProgressBar = true },
            RouteableTaskDialogResult.Retry, RouteableTaskDialogResult.Cancel);
        var html = await host.HtmlAsync();

        Assert.Contains("Working", html);
        Assert.Contains("Saving &lt;now&gt;", html);
        Assert.Contains("please wait", html);
        Assert.Contains("rt-progress", html);
        Assert.Contains(">Retry<", html);
        Assert.Contains(">Cancel<", html);

        Press(service, "Retry");
        Assert.Equal(RouteableTaskDialogResult.Retry, await task);
    }

    [Fact]
    public async Task Closing_can_keep_the_dialog_open_and_Closed_is_reported_once()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableTaskDialogService>();
        var log = new List<string>();
        var allow = false;
        var task = ((IRouteableTaskDialogService)service).ShowAsync(
            new RouteableTaskDialog { Closing = () => { log.Add("closing"); return !allow; }, Closed = () => log.Add("closed") },
            RouteableTaskDialogResult.Close);

        Press(service, "Close");
        Assert.False(task.IsCompleted);

        allow = true;
        Press(service, "Close");

        Assert.Equal(RouteableTaskDialogResult.Close, await task);
        Assert.Equal(["closing", "closing", "closed"], log);
    }

    [Fact]
    public async Task Dismissing_closes_with_None()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableTaskDialogService>();
        var task = ((IRouteableTaskDialogService)service).ShowAsync(new RouteableTaskDialog(), RouteableTaskDialogResult.OK);

        service.Dismiss();

        Assert.Equal(RouteableTaskDialogResult.None, await task);
    }

    [Fact]
    public async Task Dialogs_are_queued_and_shown_one_at_a_time()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableTaskDialogService>();
        IRouteableTaskDialogService dialogs = service;

        var first = dialogs.ShowAsync(new RouteableTaskDialog { Title = "First" }, RouteableTaskDialogResult.OK);
        var second = dialogs.ShowAsync(new RouteableTaskDialog { Title = "Second" }, RouteableTaskDialogResult.Yes);

        Assert.Equal("First", service.Current!.Parameters.Title);
        Press(service, "OK");
        Assert.Equal(RouteableTaskDialogResult.OK, await first);
        Assert.Equal("Second", service.Current!.Parameters.Title);
        Press(service, "Yes");
        Assert.Equal(RouteableTaskDialogResult.Yes, await second);
    }

    [Fact]
    public async Task Without_results_the_dialog_offers_Close()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableTaskDialogService>();
        var task = ((IRouteableTaskDialogService)service).ShowAsync(new RouteableTaskDialog());

        Assert.Equal(["Close"], service.Current!.Session.Buttons.Select(b => b.Text));
        Press(service, "Close");

        Assert.Equal(RouteableTaskDialogResult.Close, await task);
    }

    [Fact]
    public async Task Showing_a_dialog_without_a_host_or_without_parameters_fails()
    {
        var service = new RouteableTaskDialogService();

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ShowAsync(null!));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ShowAsync(new RouteableTaskDialog()));
        Assert.Contains("RouteableTaskDialogHost", exception.Message);
    }
}
