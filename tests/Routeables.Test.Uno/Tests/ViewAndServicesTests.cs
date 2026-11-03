using Microsoft.VisualStudio.TestTools.UnitTesting;
using Routeables.Abstraction;
using Routeables.Services;
using Routeables.Routing;
using Routeables.Uno;
using Routeables.Uno.Services;
using Uno.UI.RuntimeTests;

namespace Routeables.Test.Uno.Tests;

/// <summary>The view base class, the dispatcher and the services the application registered, on the platform.</summary>
[TestClass]
[RunsOnUIThread]
public class ViewAndServicesTests
{
    private sealed class CountingView : RouteableFor<object>
    {
        public int Subscribed { get; private set; }
        public int Unsubscribed { get; private set; }

        protected override void OnSubscribe() => Subscribed++;

        protected override void OnUnsubscribe() => Unsubscribed++;
    }

    [TestMethod]
    public async Task The_view_is_not_locked_and_can_be_left()
    {
        var view = new RouteableFor { Context = new object() };

        await view.InitializeAsync(RouteableArgs.Create());

        Assert.IsFalse(view.IsLocked);
        Assert.IsTrue(await view.CanLeaveAsync());
    }

    [TestMethod]
    public async Task OnSubscribe_runs_once_per_view_model_however_many_times_the_view_is_initialized()
    {
        var view = new CountingView { Context = new object() };

        await view.InitializeAsync(RouteableArgs.Create());
        await view.InitializeAsync(RouteableArgs.Create());

        Assert.AreEqual(1, view.Subscribed);
        Assert.AreEqual(0, view.Unsubscribed);
    }

    [TestMethod]
    public async Task A_new_view_model_unsubscribes_the_old_one_and_subscribes_the_new_one()
    {
        var view = new CountingView { Context = new object() };
        await view.InitializeAsync(RouteableArgs.Create());

        view.Context = new object();
        await view.InitializeAsync(RouteableArgs.Create());

        Assert.AreEqual(2, view.Subscribed);
        Assert.AreEqual(1, view.Unsubscribed);
    }

    [TestMethod]
    public async Task Dispose_unsubscribes_once()
    {
        var view = new CountingView { Context = new object() };
        await view.InitializeAsync(RouteableArgs.Create());

        view.Dispose();
        view.Dispose();

        Assert.AreEqual(1, view.Unsubscribed);
    }

    [TestMethod]
    public async Task A_view_without_a_view_model_subscribes_to_nothing()
    {
        var view = new CountingView();

        await view.InitializeAsync(RouteableArgs.Create());

        Assert.AreEqual(0, view.Subscribed);
    }

    // ------------------------------------------------------------------------------------------------ the dispatcher

    [TestMethod]
    public void The_dispatcher_recognizes_the_thread_of_the_interface()
        => Assert.IsTrue(new RouteableDispatcher().CheckAccess());

    [TestMethod]
    public async Task The_dispatcher_recognizes_another_thread_and_brings_the_work_back()
    {
        var dispatcher = new RouteableDispatcher();

        var (onOther, ranOnInterface) = await Task.Run(async () =>
        {
            var other = dispatcher.CheckAccess();
            var back = await dispatcher.InvokeAsync(() => dispatcher.CheckAccess());
            return (other, back);
        });

        Assert.IsFalse(onOther);
        Assert.IsTrue(ranOnInterface);
    }

    [TestMethod]
    public async Task Post_queues_the_work_on_the_interface()
    {
        var done = false;

        new RouteableDispatcher().Post(() => done = true);

        await TestHost.UntilAsync(() => done);
    }

    // ------------------------------------------------------------------------------------------------ the services of the application

    [TestMethod]
    public void The_application_registered_the_routeables_services()
    {
        var services = TestHost.Services;

        Assert.IsNotNull(services.GetService(typeof(IRouter)));
        Assert.IsNotNull(services.GetService(typeof(IMainViewModel)));
        Assert.IsNotNull(services.GetService(typeof(IRouteableDialogService)));
        Assert.IsNotNull(services.GetService(typeof(IMessageBoxService)));
        Assert.IsNotNull(services.GetService(typeof(IRouteableNotificationService)));
    }

    [TestMethod]
    public void The_routers_of_the_regions_are_not_the_default_router()
    {
        Assert.AreNotSame(TestHost.Router(), TestHost.Router(App.Side));
        Assert.AreSame(TestHost.Router(App.Side), TestHost.Router(App.Side));
    }

    [TestMethod]
    public async Task A_route_creates_the_view_and_gives_it_its_view_model()
    {
        await TestHost.ResetAsync();

        var result = await TestHost.Services.GoToAsync("/a");

        Assert.IsTrue(result.Success);
        var view = (PageAView)TestHost.Router().CurrentView!;
        Assert.AreSame(view.ViewModel, TestHost.Router().CurrentViewModel);
    }

    [TestMethod]
    public async Task A_route_nobody_registered_fails_without_throwing()
    {
        await TestHost.ResetAsync();

        var result = await TestHost.Services.GoToAsync("/nowhere");

        Assert.IsFalse(result.Success);
    }

    [TestMethod]
    public async Task Concurrent_navigations_leave_one_consistent_history()
    {
        await TestHost.ResetAsync();

        await Task.WhenAll(Enumerable.Range(0, 10).Select(i => TestHost.Services.GoToAsync(i % 2 == 0 ? "/a" : "/b")));

        Assert.IsTrue(TestHost.Router().CurrentView is PageAView or PageBView);
    }
}
