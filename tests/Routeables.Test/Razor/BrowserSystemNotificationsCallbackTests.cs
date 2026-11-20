using Microsoft.JSInterop;
using Routeables.Blazor.Services;
using Routeables.Native;
using Routeables.Services;

namespace Routeables.Tests.Razor;

/// <summary>What the browser sends back to the page (click, close, reply), and the failures of the browser side of the notifications.</summary>
public class BrowserSystemNotificationsCallbackTests
{
    private sealed class Module : IJSObjectReference
    {
        public List<(string Identifier, object?[] Args)> Calls { get; } = [];
        public string? FailOn { get; set; }
        public Exception? FailOnDispose { get; set; }
        public int Disposals { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Calls.Add((identifier, args ?? []));
            if (identifier == FailOn)
                throw new JSException("the browser refused");
            object? result = identifier switch { "permission" => "granted", "register" => true, _ => null };
            return new((TValue)result!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync()
        {
            Disposals++;
            return FailOnDispose is null ? ValueTask.CompletedTask : throw FailOnDispose;
        }
    }

    private sealed class Js(IJSObjectReference module) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => new((TValue)(object)module);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }

    private static BrowserSystemNotifications Create(Module module, AppNotificationOptions? options = null)
        => new(new Js(module), options ?? new AppNotificationOptions());

    // The object that the page gave to the browser (DotNetObjectReference of a private class): call one of its methods like the browser does.
    private static void FromBrowser(Module module, string method, params object?[] args)
    {
        var reference = module.Calls.Last(c => c.Identifier == "registrations").Args[0]!;
        var target = reference.GetType().GetProperty("Value")!.GetValue(reference)!;
        target.GetType().GetMethod(method)!.Invoke(target, args);
    }

    private static async Task<(BrowserSystemNotifications System, IRouteableSystemNotification Notification, List<RouteableSystemNotificationEventArgs> Completed)> Shown(Module module)
    {
        var system = Create(module);
        var notification = (await system.CreateNotificationAsync(null))!;
        await notification.ShowAsync();
        var completed = new List<RouteableSystemNotificationEventArgs>();
        system.NotificationCompleted += (_, e) => completed.Add(e);
        return (system, notification, completed);
    }

    [Fact]
    public async Task A_click_on_a_button_reports_the_notification_as_activated_with_the_button()
    {
        var module = new Module();
        var (system, notification, completed) = await Shown(module);

        FromBrowser(module, "OnClick", $$"""{"type":"HandleNotificationClick","id":{{notification.Id}},"action":"open"}""");

        var only = Assert.Single(completed);
        Assert.True(only.IsActivated);
        Assert.False(only.IsCancelled);
        Assert.Equal("open", only.ActionTag);
        Assert.Null(only.UserData);
        Assert.Empty(system.ActiveNotifications());
    }

    [Fact]
    public async Task A_click_on_the_notification_itself_has_no_button()
    {
        var module = new Module();
        var (_, notification, completed) = await Shown(module);

        FromBrowser(module, "OnClick", $$"""{"id":{{notification.Id}},"action":5}""");

        Assert.Null(Assert.Single(completed).ActionTag);
    }

    [Fact]
    public async Task A_close_by_the_browser_reports_the_notification_as_cancelled()
    {
        var module = new Module();
        var (_, notification, completed) = await Shown(module);

        FromBrowser(module, "OnClose", $$"""{"id":{{notification.Id}}}""");

        var only = Assert.Single(completed);
        Assert.True(only.IsCancelled);
        Assert.False(only.IsActivated);
    }

    [Fact]
    public async Task A_reply_reports_the_notification_as_activated_with_the_text_of_the_reply()
    {
        var module = new Module();
        var (_, notification, completed) = await Shown(module);

        FromBrowser(module, "OnReply", $$"""{"id":{{notification.Id}},"action":"reply"}""", "thank you");

        var only = Assert.Single(completed);
        Assert.True(only.IsActivated);
        Assert.Equal("thank you", only.UserData);
        Assert.Equal("reply", only.ActionTag);
    }

    [Theory]
    [InlineData("""{"type":"HandleNotificationClick"}""")]       // no id
    [InlineData("""{"id":"three"}""")]                             // an id that is not a number
    [InlineData("""{"id":-1}""")]                                  // an id that is not an unsigned number
    [InlineData("""{"id":999999}""")]                              // a notification that is not shown (already closed)
    public async Task A_message_that_names_no_notification_that_is_shown_is_ignored(string json)
    {
        var module = new Module();
        var (system, notification, completed) = await Shown(module);

        FromBrowser(module, "OnClick", json);

        Assert.Empty(completed);
        Assert.Single(system.ActiveNotifications());
        Assert.Equal(notification.Id, system.ActiveNotifications().Keys.Single());
    }

    [Fact]
    public async Task A_notification_that_the_browser_refuses_is_not_kept_and_the_failure_is_given_back()
    {
        var module = new Module { FailOn = "show" };
        var system = Create(module);
        var notification = (await system.CreateNotificationAsync(null))!;

        await Assert.ThrowsAsync<JSException>(notification.ShowAsync);

        Assert.Empty(system.ActiveNotifications());
    }

    [Fact]
    public async Task CloseAll_asks_the_browser_to_close_what_it_shows()
    {
        var module = new Module();
        var (system, _, completed) = await Shown(module);

        system.CloseAll();

        Assert.Contains(module.Calls, c => c.Identifier == "closeAll");
        Assert.Single(completed);
    }

    [Fact]
    public async Task Disposing_closes_what_is_shown_when_the_options_say_so_and_gives_the_module_back()
    {
        var module = new Module();
        var system = Create(module, new AppNotificationOptions { ClearOnAppClose = true });
        await (await system.CreateNotificationAsync(null))!.ShowAsync();
        var completed = 0;
        system.NotificationCompleted += (_, _) => completed++;

        await system.DisposeAsync();

        Assert.Equal(1, completed);
        Assert.Equal(1, module.Disposals);
    }

    [Fact]
    public async Task Disposing_leaves_what_is_shown_when_the_options_say_not_to_clear()
    {
        var module = new Module();
        var system = Create(module, new AppNotificationOptions { ClearOnAppClose = false });
        await (await system.CreateNotificationAsync(null))!.ShowAsync();

        await system.DisposeAsync();

        Assert.DoesNotContain(module.Calls, c => c.Identifier == "closeAll");
        Assert.Equal(1, module.Disposals);
    }

    [Theory]
    [InlineData(typeof(JSDisconnectedException))]
    [InlineData(typeof(JSException))]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(TaskCanceledException))]
    public async Task A_circuit_that_is_gone_does_not_fail_the_disposal(Type failure)
    {
        var module = new Module { FailOnDispose = (Exception)Activator.CreateInstance(failure, "gone")! };
        var system = Create(module);
        await system.CreateNotificationAsync(null);

        await system.DisposeAsync();

        Assert.Equal(1, module.Disposals);
    }

    [Fact]
    public async Task A_service_without_any_call_can_be_disposed()
    {
        await Create(new Module()).DisposeAsync();
    }

    [Fact]
    public async Task A_module_that_fails_to_be_given_back_after_a_failed_start_does_not_fail_the_call()
    {
        var module = new Module { FailOn = "register", FailOnDispose = new JSDisconnectedException("gone") };
        var system = Create(module);

        Assert.Null(await system.CreateNotificationAsync(null));
    }
}
