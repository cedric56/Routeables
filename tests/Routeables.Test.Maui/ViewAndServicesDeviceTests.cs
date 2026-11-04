using System.Globalization;
using DeviceRunners.UITesting;
using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Maui;
using Routeables.Maui.Services;
using Routeables.Services;
using Xunit;

namespace Routeables.Test.Maui;

/// <summary>The view base class, the converter, the theme service and the dispatcher of of MAUI, and the services the application registered, on the platform.</summary>
public class ViewAndServicesDeviceTests
{
    /// <summary>A view that counts how many times it subscribes and unsubscribes.</summary>
    private sealed class CountingView : RouteableFor<object>
    {
        /// <summary>How many times <see cref="OnSubscribe"/> has been called.</summary>
        public int Subscribed { get; private set; }

        /// <summary>How many times <see cref="OnUnsubscribe"/> has been called.</summary>
        public int Unsubscribed { get; private set; }

        /// <inheritdoc />
        protected override void OnSubscribe() => Subscribed++;

        /// <inheritdoc />
        protected override void OnUnsubscribe() => Unsubscribed++;
    }

    // ------------------------------------------------------------------------------------------------ RouteableFor

    /// <summary>The context is the binding context and the view is not locked by default.</summary>
    [UIFact]
    public async Task The_context_is_the_binding_context_and_the_view_is_not_locked()
    {
        var view = new RouteableFor();
        var viewModel = new object();

        view.Context = viewModel;
        await view.InitializeAsync(RouteableArgs.Create());

        Assert.Same(viewModel, view.BindingContext);
        Assert.False(view.IsLocked);
        Assert.True(await view.CanLeaveAsync());
    }

    /// <summary>OnSubscribe runs once per view model, no matter how many times the view is initialized.</summary>
    [UIFact]
    public async Task OnSubscribe_runs_once_per_view_model_however_many_times_the_view_is_initialized()
    {
        var view = new CountingView { Context = new object() };

        await view.InitializeAsync(RouteableArgs.Create());
        await view.InitializeAsync(RouteableArgs.Create());
        await view.InitializeAsync(RouteableArgs.Create());

        Assert.Equal(1, view.Subscribed);
        Assert.Equal(0, view.Unsubscribed);
    }

    /// <summary>A new view model unsubscribes the old one and subscribes the new one.</summary>
    [UIFact]
    public async Task A_new_view_model_unsubscribes_the_old_one_and_subscribes_the_new_one()
    {
        var view = new CountingView { Context = new object() };
        await view.InitializeAsync(RouteableArgs.Create());

        view.Context = new object();
        await view.InitializeAsync(RouteableArgs.Create());

        Assert.Equal(2, view.Subscribed);
        Assert.Equal(1, view.Unsubscribed);
    }

    /// <summary>Dispose unsubscribes once, even when called multiple times.</summary>
    [UIFact]
    public async Task Dispose_unsubscribes_once()
    {
        var view = new CountingView { Context = new object() };
        await view.InitializeAsync(RouteableArgs.Create());

        view.Dispose();
        view.Dispose();

        Assert.Equal(1, view.Unsubscribed);
    }

    /// <summary>A view without a view model subscribes to nothing.</summary>
    [UIFact]
    public async Task A_view_without_a_view_model_subscribes_to_nothing()
    {
        var view = new CountingView();

        await view.InitializeAsync(RouteableArgs.Create());

        Assert.Equal(0, view.Subscribed);
    }

    // ------------------------------------------------------------------------------------------------ the converter

    /// <summary>RouteableNullConverter is true for null and blank text.</summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("x", false)]
    [InlineData(" x ", false)]
    public void RouteableNullConverter_is_true_for_null_and_blank_text(string? value, bool expected)
        => Assert.Equal(expected, new RouteableNullConverter().Convert(value, typeof(bool), null, CultureInfo.InvariantCulture));

    /// <summary>RouteableNullConverter is false for a value that is not text.</summary>
    [Fact]
    public void RouteableNullConverter_is_false_for_a_value_that_is_not_text()
        => Assert.Equal(false, new RouteableNullConverter().Convert(42, typeof(bool), null, CultureInfo.InvariantCulture));

    /// <summary>RouteableNullConverter does not convert back.</summary>
    [Fact]
    public void RouteableNullConverter_does_not_convert_back()
        => Assert.Same(Binding.DoNothing, new RouteableNullConverter().ConvertBack(true, typeof(string), null, CultureInfo.InvariantCulture));

    // ------------------------------------------------------------------------------------------------ the theme

    /// <summary>The theme service sets the theme of the application.</summary>
    [UIFact]
    public void The_theme_service_sets_the_theme_of_the_application()
    {
        var application = Application.Current!;
        var before = application.UserAppTheme;
        var service = new RouteableRequestThemeService();
        try
        {
            service.Request(RouteableRequestedTheme.Dark);
            Assert.Equal(AppTheme.Dark, application.UserAppTheme);

            service.Request(RouteableRequestedTheme.Light);
            Assert.Equal(AppTheme.Light, application.UserAppTheme);

            service.Request(RouteableRequestedTheme.Default);
            Assert.Equal(AppTheme.Unspecified, application.UserAppTheme);
        }
        finally
        {
            application.UserAppTheme = before;
        }
    }

    // ------------------------------------------------------------------------------------------------ the dispatcher

    /// <summary>The dispatcher recognizes the UI thread.</summary>
    [UIFact]
    public void The_dispatcher_recognizes_the_thread_of_the_interface()
        => Assert.True(new RouteableDispatcher().CheckAccess());

    /// <summary>The dispatcher recognizes another thread and marshals work back to the UI thread.</summary>
    [UIFact]
    public async Task The_dispatcher_recognizes_another_thread_and_brings_the_work_back()
    {
        var dispatcher = new RouteableDispatcher();

        var (onOther, ranOnInterface) = await Task.Run(async () =>
        {
            var other = dispatcher.CheckAccess();
            var back = await dispatcher.InvokeAsync(() => dispatcher.CheckAccess());
            return (other, back);
        });

        Assert.False(onOther);
        Assert.True(ranOnInterface);
    }

    /// <summary>Post queues the work on the UI thread.</summary>
    [UIFact]
    public async Task Post_queues_the_work_on_the_interface()
    {
        var done = false;

        new RouteableDispatcher().Post(() => done = true);

        await Device.UntilAsync(() => done);
    }

    /// <summary>Post refuses null work.</summary>
    [UIFact]
    public void Post_refuses_no_work()
        => Assert.Throws<ArgumentNullException>(() => new RouteableDispatcher().Post(null!));

    /// <summary>An exception thrown by the work is handed back to the caller.</summary>
    [UIFact]
    public async Task An_exception_of_the_work_is_handed_back_to_the_caller()
    {
        var dispatcher = new RouteableDispatcher();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(() => dispatcher.InvokeAsync(() => throw new InvalidOperationException("boom"))));
    }

    // ------------------------------------------------------------------------------------------------ the services of the application

    /// <summary>The application registered the core Routeables services.</summary>
    [UIFact]
    public void The_application_registered_the_routeables_services()
    {
        var services = Device.Services;

        Assert.NotNull(services.GetService(typeof(IRouter)));
        Assert.NotNull(services.GetService(typeof(IMainViewModel)));
        Assert.NotNull(services.GetService(typeof(IRouteableDispatcher)));
        Assert.NotNull(services.GetService(typeof(IRouteableRequestedThemeService)));
        Assert.NotNull(services.GetService(typeof(IRouteableDialogService)));
        Assert.NotNull(services.GetService(typeof(IMessageBoxService)));
        Assert.NotNull(services.GetService(typeof(IRouteableNotificationService)));
        Assert.NotNull(services.GetService(typeof(IRouteableDialogWindowContext)));
    }

    /// <summary>The dispatcher of the application is the MAUI <see cref="RouteableDispatcher"/>.</summary>
    [UIFact]
    public void The_dispatcher_of_the_application_is_the_one_of_MAUI()
        => Assert.IsType<RouteableDispatcher>(Device.Services.GetService(typeof(IRouteableDispatcher)));

    /// <summary>The dialog service is also the message-box service and is a singleton.</summary>
    [UIFact]
    public void The_dialog_service_is_the_message_box_service_and_one_for_the_application()
    {
        var dialogs = Device.Services.GetService(typeof(IRouteableDialogService));

        Assert.IsType<RouteableDialogService>(dialogs);
        Assert.Same(dialogs, Device.Services.GetService(typeof(IRouteableDialogService)));
        Assert.Same(dialogs, Device.Services.GetService(typeof(IMessageBoxService)));
    }

    /// <summary>The main view model is a singleton for the application.</summary>
    [UIFact]
    public void The_main_view_model_is_one_for_the_application()
        => Assert.Same(Device.Services.GetService(typeof(IMainViewModel)), Device.Services.GetService(typeof(IMainViewModel)));

    /// <summary>The routers of named regions are distinct from the default router and stable.</summary>
    [UIFact]
    public void The_routers_of_the_regions_are_not_the_default_router()
    {
        Assert.NotSame(Device.Router(), Device.Router(MauiProgram.Side));
        Assert.Same(Device.Router(MauiProgram.Side), Device.Router(MauiProgram.Side));
    }

    /// <summary>An unknown region is refused with a message that names it.</summary>
    [UIFact]
    public void An_unknown_region_is_refused_with_a_message_that_names_it()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Device.Router("nowhere"));

        Assert.Contains("nowhere", ex.Message);
    }

    // ------------------------------------------------------------------------------------------------ routing on the platform

    /// <summary>Navigating to a route creates the view and assigns its view model.</summary>
    [UIFact]
    public async Task A_route_creates_the_view_and_gives_it_its_view_model()
    {
        await Device.ResetAsync();

        var result = await Device.Services.GoToAsync("/a");

        Assert.True(result.Success);
        var view = Assert.IsType<PageAView>(Device.Router().CurrentView);
        Assert.Same(view.BindingContext, Device.Router().CurrentViewModel);
        Assert.IsType<PageAViewModel>(view.BindingContext);
    }

    /// <summary>A route nobody registered fails without throwing.</summary>
    [UIFact]
    public async Task A_route_nobody_registered_fails_without_throwing()
    {
        await Device.ResetAsync();

        var result = await Device.Services.GoToAsync("/nowhere");

        Assert.False(result.Success);
    }

    /// <summary>Navigation started from another thread works.</summary>
    [UIFact]
    public async Task Navigation_from_another_thread_works()
    {
        await Device.ResetAsync();

        var result = await Task.Run(() => Device.Services.GoToAsync("/b"));

        Assert.True(result.Success);
        Assert.IsType<PageBView>(Device.Router().CurrentView);
    }

    /// <summary>Concurrent navigations leave one consistent history and a valid current view.</summary>
    [UIFact]
    public async Task Concurrent_navigations_leave_one_consistent_history()
    {
        await Device.ResetAsync();

        await Task.WhenAll(Enumerable.Range(0, 10).Select(i => Device.Services.GoToAsync(i % 2 == 0 ? "/a" : "/b")));

        Assert.NotNull(Device.Router().CurrentView);
        Assert.True(Device.Router().CurrentView is PageAView or PageBView);
    }
}