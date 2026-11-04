using Microsoft.Extensions.DependencyInjection;
using Routeables.Services;
using Uno.UI.RuntimeTests;

namespace Routeables.Test.Uno.Tests;

/// <summary>
/// The toast notifications of Uno: <c>InfoBar</c>s stacked in a popup of the window, with the severity of their type, closed after their
/// expiration or by the user, each telling once that it closed.
/// </summary>
[TestClass]
[RunsOnUIThread]
public class NotificationTests
{
    /// <summary>The XamlRoot of the test host, used as the owner for notifications.</summary>
    private static XamlRoot Root => MainPage.Host.XamlRoot!;

    /// <summary>Resolves the notification service from the shared test container.</summary>
    private static IRouteableNotificationService Service => TestHost.Services.GetRequiredService<IRouteableNotificationService>();

    /// <summary>Returns all open <see cref="InfoBar"/> instances currently shown in popups of the root.</summary>
    private static List<InfoBar> Bars() =>
        [.. VisualTreeHelper.GetOpenPopupsForXamlRoot(Root).Select(p => p.Child).OfType<Grid>()
            .SelectMany(g => g.Children.OfType<StackPanel>()).SelectMany(s => s.Children.OfType<InfoBar>())];

    /// <summary>Closes every open info bar and waits until none remain.</summary>
    private static async Task CloseAllAsync()
    {
        foreach (var bar in Bars()) bar.IsOpen = false;
        await TestHost.UntilAsync(() => Bars().Count == 0);
    }

    /// <summary>A notification is shown as an info bar with its title, message and severity.</summary>
    [TestMethod]
    public async Task A_notification_is_an_info_bar_with_its_title_and_message()
    {
        Service.Show("Saved", "Your changes are saved.", RouteableNotificationType.Success, RouteableNotificationDefaults.Never);
        try
        {
            await TestHost.UntilAsync(() => Bars().Count == 1);

            var bar = Bars().Single();
            Assert.AreEqual("Saved", bar.Title);
            Assert.AreEqual("Your changes are saved.", bar.Message);
            Assert.AreEqual(InfoBarSeverity.Success, bar.Severity);
            Assert.IsTrue(bar.IsClosable);
        }
        finally
        {
            await CloseAllAsync();
        }
    }

    /// <summary>Each notification type maps to its corresponding <see cref="InfoBarSeverity"/>.</summary>
    [TestMethod]
    public async Task Each_type_has_its_own_severity()
    {
        var types = new (RouteableNotificationType Type, InfoBarSeverity Severity)[]
        {
            (RouteableNotificationType.Information, InfoBarSeverity.Informational),
            (RouteableNotificationType.Success, InfoBarSeverity.Success),
            (RouteableNotificationType.Warning, InfoBarSeverity.Warning),
            (RouteableNotificationType.Error, InfoBarSeverity.Error),
        };

        foreach (var (type, severity) in types)
        {
            Service.Show("T", type.ToString(), type, RouteableNotificationDefaults.Never);
            await TestHost.UntilAsync(() => Bars().Any(b => b.Message == type.ToString()));

            Assert.AreEqual(severity, Bars().Single(b => b.Message == type.ToString()).Severity);
        }

        await CloseAllAsync();
    }

    /// <summary>Multiple notifications stack up in a single popup.</summary>
    [TestMethod]
    public async Task Notifications_stack_up_in_one_popup()
    {
        Service.Show("A", "a", expiration: RouteableNotificationDefaults.Never);
        Service.Show("B", "b", expiration: RouteableNotificationDefaults.Never);
        try
        {
            await TestHost.UntilAsync(() => Bars().Count == 2);

            CollectionAssert.AreEquivalent(new[] { "A", "B" }, Bars().Select(b => b.Title).ToArray());
        }
        finally
        {
            await CloseAllAsync();
        }
    }

    /// <summary>A notification closes after its expiration and invokes its close callback exactly once.</summary>
    [TestMethod]
    public async Task A_notification_closes_after_its_expiration_and_tells_once()
    {
        var closed = 0;
        Service.Show("T", "M", expiration: TimeSpan.FromMilliseconds(200), onClose: () => closed++);

        await TestHost.UntilAsync(() => closed == 1);
        await TestHost.UntilAsync(() => Bars().Count == 0);

        Assert.AreEqual(1, closed);
    }

    /// <summary>A notification the user closes invokes its close callback.</summary>
    [TestMethod]
    public async Task A_notification_the_user_closes_tells_that_it_closed()
    {
        var closed = 0;
        Service.Show("T", "M", expiration: RouteableNotificationDefaults.Never, onClose: () => closed++);
        await TestHost.UntilAsync(() => Bars().Count == 1);

        Bars().Single().IsOpen = false;

        await TestHost.UntilAsync(() => closed == 1);
        Assert.AreEqual(1, closed);
    }

    /// <summary>A notification that never expires stays open until closed explicitly.</summary>
    [TestMethod]
    public async Task A_notification_that_never_expires_stays_open()
    {
        Service.Show("T", "M", expiration: RouteableNotificationDefaults.Never);
        try
        {
            await TestHost.UntilAsync(() => Bars().Count == 1);
            await Task.Delay(300);

            Assert.AreEqual(1, Bars().Count);
        }
        finally
        {
            await CloseAllAsync();
        }
    }

    /// <summary>Without a native manager there are no system notifications to list.</summary>
    [TestMethod]
    public void Without_a_native_manager_there_is_no_system_notification_to_list()
    {
        Assert.AreEqual(0, Service.ActiveNotifications().Count);
    }
}