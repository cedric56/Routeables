using Microsoft.JSInterop;
using Routeables.Razor.Services;
using Routeables.Services;

namespace Routeables.Tests.Razor;

/// <summary>The system notifications of Blazor (the notification center of the browser) on a JavaScript runtime that records what it is asked.</summary>
public class BrowserSystemNotificationsTests
{
    private sealed class FakeModule(string permission, string afterRequest) : IJSObjectReference
    {
        public List<(string Identifier, object?[] Args)> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Calls.Add((identifier, args ?? []));
            object? result = identifier switch
            {
                "permission" => permission,
                "requestPermission" => afterRequest,
                "register" => true,
                _ => null
            };
            return new((TValue)result!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeJs(IJSObjectReference? module, Exception? failure = null) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (failure is not null) throw failure;
            return new((TValue)(object)module!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }

    private static BrowserSystemNotifications Create(FakeModule module, AppNotificationOptions? options = null)
        => new(new FakeJs(module), options ?? new AppNotificationOptions());

    [Fact]
    public async Task A_granted_permission_creates_notifications_with_increasing_ids()
    {
        var system = Create(new FakeModule("granted", "granted"));

        var first = await system.CreateNotificationAsync("animals");
        var second = await system.CreateNotificationAsync(null);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("animals", first.Category);
        Assert.Equal(first.Id + 1, second.Id);
    }

    [Theory]
    [InlineData("denied", "denied")]
    [InlineData("unsupported", "unsupported")]
    [InlineData("default", "denied")]
    public async Task A_permission_that_is_not_granted_creates_nothing(string permission, string afterRequest)
    {
        var system = Create(new FakeModule(permission, afterRequest));

        Assert.Null(await system.CreateNotificationAsync("animals"));
    }

    [Fact]
    public async Task A_permission_not_asked_yet_is_requested_and_then_granted()
    {
        var module = new FakeModule("default", "granted");
        var system = Create(module);

        Assert.NotNull(await system.CreateNotificationAsync(null));
        Assert.Contains(module.Calls, c => c.Identifier == "requestPermission");
    }

    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(JSDisconnectedException))]
    public async Task A_page_that_cannot_call_JavaScript_yet_creates_nothing(Type failure)
    {
        // Prerendering, or a circuit that is gone: JavaScript cannot be called, which is not an error of the application.
        var exception = (Exception)Activator.CreateInstance(failure, "no javascript")!;
        var system = new BrowserSystemNotifications(new FakeJs(null, exception), new AppNotificationOptions());

        Assert.Null(await system.CreateNotificationAsync(null));
        Assert.Empty(system.ActiveNotifications());
    }

    [Fact]
    public async Task The_service_worker_is_registered_at_the_address_of_the_options_and_not_when_it_is_null()
    {
        var withWorker = new FakeModule("granted", "granted");
        await Create(withWorker, new AppNotificationOptions { ServiceWorkerUrl = "/worker.js" }).InitializeAsync();
        var without = new FakeModule("granted", "granted");
        await Create(without, new AppNotificationOptions { ServiceWorkerUrl = null }).InitializeAsync();

        Assert.Equal("/worker.js", withWorker.Calls.Single(c => c.Identifier == "register").Args.Single());
        Assert.Null(without.Calls.Single(c => c.Identifier == "register").Args.Single());
        Assert.Contains(withWorker.Calls, c => c.Identifier == "registrations");
    }

    [Fact]
    public async Task InitializeAsync_does_not_ask_for_the_permission_when_the_options_say_not_to()
    {
        var module = new FakeModule("default", "granted");

        await Create(module, new AppNotificationOptions { RequestPermissionOnInitialize = false }).InitializeAsync();

        Assert.Empty(module.Calls);
    }

    [Fact]
    public async Task A_shown_notification_is_active_and_is_given_to_the_page_with_its_icon_and_actions()
    {
        var module = new FakeModule("granted", "granted");
        var system = Create(module, new AppNotificationOptions { DefaultIcon = "/icon.png" });
        var notification = (await system.CreateNotificationAsync("animals"))!;
        notification.Title = "New animal";
        notification.Message = "A polar bear";
        notification.Expiration = TimeSpan.FromSeconds(2);
        notification.SetActions([new RouteableSystemNotificationAction("Open", "open") { Icon = "/open.png" }]);

        await notification.ShowAsync();

        Assert.Same(notification, system.ActiveNotifications()[notification.Id]);
        var show = module.Calls.Single(c => c.Identifier == "show").Args;
        Assert.Equal(notification.Id, show[0]);
        Assert.Equal("New animal", show[1]);
        Assert.Equal("A polar bear", show[2]);
        Assert.Equal("/icon.png", show[3]);                                  // the notification has no icon: the default one
        Assert.Equal(2000d, show[5]);
        Assert.Equal(new[] { "Open", "open", "/open.png" }, (string[])show[6]!);
    }

    [Fact]
    public async Task Closing_a_notification_withdraws_it_and_tells_it_was_cancelled_once()
    {
        var module = new FakeModule("granted", "granted");
        var system = Create(module);
        var notification = (await system.CreateNotificationAsync(null))!;
        await notification.ShowAsync();
        var completed = new List<RouteableSystemNotificationEventArgs>();
        system.NotificationCompleted += (_, e) => completed.Add(e);

        await notification.CloseAsync();
        await notification.CloseAsync();

        Assert.Empty(system.ActiveNotifications());
        var only = Assert.Single(completed);
        Assert.Equal(notification.Id, only.NotificationId);
        Assert.True(only.IsCancelled);
        Assert.False(only.IsActivated);
        Assert.Contains(module.Calls, c => c.Identifier == "close");
    }

    [Fact]
    public async Task CloseAll_tells_every_active_notification_was_cancelled()
    {
        var module = new FakeModule("granted", "granted");
        var system = Create(module);
        var first = (await system.CreateNotificationAsync(null))!;
        var second = (await system.CreateNotificationAsync(null))!;
        await first.ShowAsync();
        await second.ShowAsync();
        var ids = new List<uint>();
        system.NotificationCompleted += (_, e) => ids.Add(e.NotificationId!.Value);

        system.CloseAll();

        Assert.Empty(system.ActiveNotifications());
        Assert.Equal([first.Id, second.Id], ids.Order());
    }

    [Fact]
    public async Task The_base_class_gives_the_system_notifications_to_a_service_that_only_writes_Show()
    {
        var module = new FakeModule("granted", "granted");
        var system = new RouteableSystemNotifications(Create(module));
        var service = new ToastsOnly(system);

        var notification = await service.CreateNotificationAsync("animals");

        Assert.NotNull(notification);
        Assert.Empty(service.ActiveNotifications());
        await notification.ShowAsync();
        Assert.Single(service.ActiveNotifications());
    }

    [Fact]
    public async Task The_base_class_without_system_notifications_has_harmless_defaults()
    {
        var service = new ToastsOnly(null);
        var raised = false;
        service.NotificationCompleted += (_, _) => raised = true;

        await service.InitializeAsync();
        service.CloseAll();

        Assert.Null(await service.CreateNotificationAsync(null));
        Assert.Empty(service.ActiveNotifications());
        Assert.False(raised);
    }

    private sealed class ToastsOnly(RouteableSystemNotifications? system) : RouteableNotificationServiceBase(system)
    {
        public override void Show(string title, string message, RouteableNotificationType type = RouteableNotificationType.Information,
            TimeSpan? expiration = null, Action? onClick = null, Action? onClose = null)
        {
        }
    }
}
