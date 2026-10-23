using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Razor;
using Routeables.Razor.Services;
using Routeables.Services;

namespace Routeables.Tests.Razor;

public class DialogServiceTests
{
    private static Task<RazorHostHarness<RouteableDialogHost>> StartAsync() =>
        RazorHostHarness<RouteableDialogHost>.StartAsync(services =>
        {
            services.AddScoped<RouteableDialogService>();
            services.AddScoped<IRouteableDialogService>(sp => sp.GetRequiredService<RouteableDialogService>());
        });

    [Fact]
    public async Task ShowYesNoCancel_maps_the_three_results()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableDialogService>();
        IRouteableDialogService dialogs = service;

        var yes = dialogs.ShowYesNoCancelAsync("Continue?");
        service.Complete(0);
        var no = dialogs.ShowYesNoCancelAsync("Continue?");
        service.Complete(1);
        var dismissed = dialogs.ShowYesNoCancelAsync("Continue?");
        service.Complete(service.Current!.DismissResult);

        Assert.Equal(0, await yes);
        Assert.Equal(1, await no);
        Assert.Equal(2, await dismissed);
    }

    [Fact]
    public async Task ShowYesNoAbort_maps_the_three_results_and_dismissal_means_abort()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableDialogService>();
        IRouteableDialogService dialogs = service;

        var yes = dialogs.ShowYesNoAbortAsync("m");
        service.Complete(0);
        var no = dialogs.ShowYesNoAbortAsync("m");
        service.Complete(1);
        var dismissed = dialogs.ShowYesNoAbortAsync("m");
        service.Complete(service.Current!.DismissResult);

        Assert.Equal(0, await yes);
        Assert.Equal(1, await no);
        Assert.Equal(2, await dismissed);
    }

    [Fact]
    public async Task ShowYesNo_returns_true_only_for_the_yes_response()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableDialogService>();
        IRouteableDialogService dialogs = service;

        var yes = dialogs.ShowYesNoAsync("Continue?");
        service.Complete(0);
        var no = dialogs.ShowYesNoAsync("Continue?");
        service.Complete(1);
        var dismissed = dialogs.ShowYesNoAsync("Continue?");
        service.Complete(service.Current!.DismissResult);

        Assert.True(await yes);
        Assert.False(await no);
        Assert.False(await dismissed);
    }

    [Fact]
    public async Task ShowOkCancel_and_ShowOkAbort_return_true_only_for_ok()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableDialogService>();
        IRouteableDialogService dialogs = service;

        var ok = dialogs.ShowOkCancelAsync("m");
        service.Complete(0);
        var cancelled = dialogs.ShowOkCancelAsync("m");
        service.Complete(service.Current!.DismissResult);
        var okAbort = dialogs.ShowOkAbortAsync("m");
        service.Complete(0);
        var aborted = dialogs.ShowOkAbortAsync("m");
        service.Complete(1);

        Assert.True(await ok);
        Assert.False(await cancelled);
        Assert.True(await okAbort);
        Assert.False(await aborted);
    }

    [Fact]
    public async Task The_host_renders_the_title_buttons_and_encodes_the_message()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableDialogService>();

        var task = ((IRouteableDialogService)service).ShowYesNoAsync("<b>Delete</b>\nReally?", "Confirm");
        var html = await host.HtmlAsync();

        Assert.Contains("Confirm", html);
        Assert.Contains("&lt;b&gt;Delete&lt;/b&gt;", html);
        Assert.DoesNotContain("<b>Delete</b>", html);
        Assert.Contains(">Yes<", html);
        Assert.Contains(">No<", html);

        service.Complete(0);
        Assert.True(await task);
        Assert.DoesNotContain("rt-dialog", await host.HtmlAsync());
    }

    [Fact]
    public async Task ShowError_shows_the_exception_message_with_a_single_ok_button()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableDialogService>();

        var task = ((IRouteableDialogService)service).ShowErrorAsync(new InvalidOperationException("boom"), "Oops");

        Assert.Equal("Oops", service.Current!.Title);
        Assert.Equal("boom", service.Current.Message);
        Assert.Single(service.Current.Buttons);
        service.Complete(0);
        await task;
    }

    [Fact]
    public async Task Dialogs_are_shown_one_at_a_time_in_the_order_they_were_requested()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableDialogService>();
        IRouteableDialogService dialogs = service;

        var first = dialogs.ShowOkAsync("one", "First");
        var second = dialogs.ShowOkAsync("two", "Second");

        Assert.Equal("First", service.Current!.Title);
        Assert.False(second.IsCompleted);

        service.Complete(0);
        await first;
        Assert.Equal("Second", service.Current!.Title);

        service.Complete(0);
        await second;
        Assert.Null(service.Current);
    }

    [Fact]
    public async Task Showing_a_dialog_without_a_host_fails_instead_of_waiting_forever()
    {
        var service = new RouteableDialogService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ShowOkAsync("message"));

        Assert.Contains("RouteableDialogHost", exception.Message);
    }
}
