using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Razor;
using Routeables.Razor.Services;
using Routeables.Services;

namespace Routeables.Tests.Razor;

public class ContentDialogServiceTests
{
    private sealed class RecordingCommand(Action<object?> execute, bool canExecute = true) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => canExecute;

        public void Execute(object? parameter) => execute(parameter);
    }

    // Presses the button with this text on the dialog being shown.
    private static bool Press(RouteableContentDialogService service, string text) =>
        service.Current!.Session.Buttons.Single(b => b.Text == text).Press();

    private static Task<RazorHostHarness<RouteableContentDialogHost>> StartAsync() =>
        RazorHostHarness<RouteableContentDialogHost>.StartAsync(services =>
        {
            services.AddScoped<RouteableContentDialogService>();
            services.AddScoped<IRouteableContentDialogService>(sp => sp.GetRequiredService<RouteableContentDialogService>());
        });

    [Fact]
    public async Task Disposing_the_service_releases_the_dialogs_that_were_still_waiting()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableContentDialogService>();
        var shown = ((IRouteableContentDialogService)service).ShowAsync(new RouteableContentDialog { Title = "First", CloseButtonText = "Close" });
        var queued = ((IRouteableContentDialogService)service).ShowAsync(new RouteableContentDialog { Title = "Second", CloseButtonText = "Close" });

        service.Dispose();      // the circuit is over

        Assert.Equal(RouteableContentDialogResult.None, await shown.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Equal(RouteableContentDialogResult.None, await queued.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Null(service.Current);
    }

    [Fact]
    public async Task The_host_renders_the_title_and_only_the_buttons_that_have_a_text()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableContentDialogService>();

        var task = ((IRouteableContentDialogService)service).ShowAsync(new RouteableContentDialog
        {
            Title = "Edit <order>",
            PrimaryButtonText = "Save",
            IsPrimaryButtonEnabled = true,
            CloseButtonText = "Close"
        });
        var html = await host.HtmlAsync();

        Assert.Contains("Edit &lt;order&gt;", html);
        Assert.Contains(">Save<", html);
        Assert.Contains(">Close<", html);
        Assert.DoesNotContain("Secondary", html);

        service.Dismiss();
        Assert.Equal(RouteableContentDialogResult.None, await task);
    }

    [Fact]
    public async Task Pressing_a_button_runs_the_command_then_asks_Closing_then_reports_Closed()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableContentDialogService>();
        var log = new List<string>();
        object? argument = null;
        var dialog = new RouteableContentDialog
        {
            PrimaryButtonText = "Save",
            Closing = result => { log.Add("closing:" + result); return false; },
            Closed = () => log.Add("closed"),
            PrimaryButtonClick = () => log.Add("click")
        };
        dialog.PrimaryButtonCommand = new RecordingCommand(p => { argument = p; log.Add("command"); });
        dialog.PrimaryButtonCommandParameter = 42;

        var task = ((IRouteableContentDialogService)service).ShowAsync(dialog);
        Press(service, "Save");

        Assert.Equal(RouteableContentDialogResult.Primary, await task);
        Assert.Equal(42, argument);
        Assert.Equal(["command", "closing:Primary", "closed"], log);
    }

    [Fact]
    public async Task The_click_callback_is_used_when_there_is_no_command()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableContentDialogService>();
        var clicked = false;
        var dialog = new RouteableContentDialog { SecondaryButtonText = "No", SecondaryButtonClick = () => clicked = true };

        var task = ((IRouteableContentDialogService)service).ShowAsync(dialog);
        Press(service, "No");

        Assert.Equal(RouteableContentDialogResult.Secondary, await task);
        Assert.True(clicked);
    }

    [Fact]
    public async Task A_command_that_cannot_execute_cannot_be_pressed()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableContentDialogService>();
        var executed = false;
        var command = new RecordingCommand(_ => executed = true, canExecute: false);

        var task = ((IRouteableContentDialogService)service).ShowAsync(
            new RouteableContentDialog { PrimaryButtonText = "Ok", PrimaryButtonCommand = command });

        Assert.False(Press(service, "Ok"));

        Assert.False(task.IsCompleted);
        Assert.False(executed);
        service.Dismiss();
        Assert.Equal(RouteableContentDialogResult.None, await task);
    }

    [Fact]
    public async Task Closing_can_keep_the_dialog_open()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableContentDialogService>();
        var closed = false;
        var task = ((IRouteableContentDialogService)service).ShowAsync(new RouteableContentDialog
        {
            PrimaryButtonText = "Yes",
            SecondaryButtonText = "No",
            Closing = result => result == nameof(RouteableContentDialogResult.Secondary),
            Closed = () => closed = true
        });

        Press(service, "No");
        Assert.False(task.IsCompleted);
        Assert.False(closed);

        Press(service, "Yes");
        Assert.Equal(RouteableContentDialogResult.Primary, await task);
        Assert.True(closed);
    }

    [Fact]
    public async Task Dismissing_closes_with_None_and_asks_Closing_first()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableContentDialogService>();
        string? asked = null;
        var task = ((IRouteableContentDialogService)service).ShowAsync(new RouteableContentDialog
        {
            Closing = result => { asked = result; return false; }
        });

        service.Dismiss();

        Assert.Equal(RouteableContentDialogResult.None, await task);
        Assert.Equal("None", asked);
    }

    [Fact]
    public async Task A_throwing_Closed_callback_is_reported_and_does_not_leave_the_dialog_stuck()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableContentDialogService>();
        var task = ((IRouteableContentDialogService)service).ShowAsync(new RouteableContentDialog
        {
            Closed = () => throw new InvalidOperationException("boom")
        });

        // The callback belongs to the application and runs from a click handler: it is reported (to Trace here, no sink is
        // registered), not thrown at the UI.
        service.Dismiss();

        Assert.True(task.IsCompleted);
        Assert.Null(service.Current);
    }

    [Fact]
    public async Task Dialogs_are_queued_and_shown_one_at_a_time()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableContentDialogService>();
        IRouteableContentDialogService dialogs = service;

        var first = dialogs.ShowAsync(new RouteableContentDialog { Title = "First", PrimaryButtonText = "Ok" });
        var second = dialogs.ShowAsync(new RouteableContentDialog { Title = "Second" });

        Assert.Equal("First", service.Current!.Parameters.Title);
        Press(service, "Ok");
        Assert.Equal(RouteableContentDialogResult.Primary, await first);
        Assert.Equal("Second", service.Current!.Parameters.Title);
        service.Dismiss();
        Assert.Equal(RouteableContentDialogResult.None, await second);
    }

    [Fact]
    public async Task The_caller_can_close_the_dialog_from_code()
    {
        await using var host = await StartAsync();
        var service = host.Get<RouteableContentDialogService>();
        var dialog = new RouteableContentDialog { Title = "Wait" };
        var task = ((IRouteableContentDialogService)service).ShowAsync(dialog);

        dialog.Close!();

        Assert.Equal(RouteableContentDialogResult.None, await task);
        Assert.Null(dialog.Close);
        Assert.Null(service.Current);
    }

    [Fact]
    public async Task Showing_a_dialog_without_a_host_or_without_parameters_fails()
    {
        var service = new RouteableContentDialogService();

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ShowAsync(null!));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ShowAsync(new RouteableContentDialog()));
        Assert.Contains("RouteableContentDialogHost", exception.Message);
    }
}
