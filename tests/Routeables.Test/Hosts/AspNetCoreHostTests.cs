using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Routing;
using Routeables.AspNetCore;
using Routeables.Services;
using Routeables.Abstraction.Registrations;
namespace Routeables.Tests.Hosts;

/// <summary>What the ASP.NET Core host does with a page address that a Razor view cannot take itself.</summary>
public class AspNetCoreHostTests
{
    public sealed class ThingView : IRouteableFor { public object? Context { get; set; } }

    /// <summary>The view model of a detail whose value comes by attribute (<c>{thing::}</c>), as <c>PandaDetailViewModel</c> of the samples.</summary>
    public sealed class ThingViewModel
    {
        public string? Name { get; private set; }

        [RouteableParameter]
        public string? Thing { get => Name; set => Name = value; }
    }

    /// <summary>A Razor view is built by MVC and has no property for the value: the host gives it to the view model's own [RouteableParameter].</summary>
    [Fact]
    public async Task A_value_by_attribute_reaches_the_property_of_the_view_model()
    {
        var services = new ServiceCollection();
        services.AddRouteablesAspNetCore(r => r.RegisterRoute<ThingView, ThingViewModel>(route: "/things/{thing::}"));
        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var router = scope.ServiceProvider.GetRequiredService<IRouter>();

        var result = await router.GoToAsync("/things/Red%20Panda", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Exception?.Message);
        Assert.Equal("Red Panda", Assert.IsType<ThingViewModel>(router.CurrentViewModel).Name);
    }

    private static (IRouteableNotificationService Service, RouteableInteractions Interactions, IServiceScope Scope) Notifications()
    {
        var services = new ServiceCollection();
        services.AddRouteablesAspNetCore();
        var scope = services.BuildServiceProvider().CreateScope();
        return (scope.ServiceProvider.GetRequiredService<IRouteableNotificationService>(), scope.ServiceProvider.GetRequiredService<RouteableInteractions>(), scope);
    }

    /// <summary>The browser has a notification center: the host creates the notification and hands it to the page that is rendered next, once.</summary>
    [Fact]
    public async Task A_system_notification_reaches_the_next_page_once()
    {
        var (service, interactions, scope) = Notifications();
        using (scope)
        {
            var notification = await service.CreateNotificationAsync("animals");
            Assert.NotNull(notification);
            notification.Title = "New animal";
            notification.Message = "A polar bear <b>arrived</b>.";
            notification.Expiration = TimeSpan.FromSeconds(5);
            await notification.ShowAsync();

            Assert.Same(notification, service.ActiveNotifications()[notification.Id]);
            var json = interactions.TakeSystemNotificationsJson();
            Assert.NotNull(json);
            Assert.Contains("\"op\":\"show\"", json);
            Assert.Contains("New animal", json);
            Assert.Contains("\"expirationMs\":5000", json);
            Assert.DoesNotContain("<b>", json);   // the page puts it in a script tag: nothing can close it
            Assert.Null(interactions.TakeSystemNotificationsJson());
        }
    }

    /// <summary>A notification closed before the page was rendered never reaches the browser; one the browser has gets a close order.</summary>
    [Fact]
    public async Task Closing_a_system_notification_withdraws_or_closes_it()
    {
        var (service, interactions, scope) = Notifications();
        using (scope)
        {
            var queued = (await service.CreateNotificationAsync(null))!;
            queued.Title = "queued";
            await queued.ShowAsync();
            await queued.CloseAsync();
            Assert.Null(interactions.TakeSystemNotificationsJson());
            Assert.Empty(service.ActiveNotifications());

            var shown = (await service.CreateNotificationAsync(null))!;
            shown.Title = "shown";
            await shown.ShowAsync();
            interactions.TakeSystemNotificationsJson();
            await shown.CloseAsync();
            Assert.Contains("\"op\":\"close\"", interactions.TakeSystemNotificationsJson());

            service.CloseAll();
            Assert.Contains("closeAll", interactions.TakeSystemNotificationsJson());
        }
    }

    /// <summary>The page reports a click or a close: the notification leaves the active ones and NotificationCompleted is raised once.</summary>
    [Fact]
    public async Task A_click_or_a_close_reported_by_the_page_raises_NotificationCompleted_once()
    {
        var (service, interactions, scope) = Notifications();
        using (scope)
        {
            var events = new List<RouteableSystemNotificationEventArgs>();
            service.NotificationCompleted += (_, e) => events.Add(e);

            var clicked = (await service.CreateNotificationAsync(null))!;
            await clicked.ShowAsync();
            var closed = (await service.CreateNotificationAsync(null))!;
            await closed.ShowAsync();

            Assert.True(interactions.CompleteSystemNotification(clicked.Id, activated: true));
            Assert.False(interactions.CompleteSystemNotification(clicked.Id, activated: false));   // a second report is ignored
            Assert.True(interactions.CompleteSystemNotification(closed.Id, activated: false));
            Assert.False(interactions.CompleteSystemNotification(9999, activated: true));          // an unknown one too

            Assert.Equal(2, events.Count);
            Assert.Equal(clicked.Id, events[0].NotificationId);
            Assert.True(events[0].IsActivated);
            Assert.False(events[0].IsCancelled);
            Assert.Equal(closed.Id, events[1].NotificationId);
            Assert.True(events[1].IsCancelled);
            Assert.False(events[1].IsActivated);
            Assert.Empty(service.ActiveNotifications());
        }
    }
}
