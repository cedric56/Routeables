using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Core;
using Routeables.Locking;
using Routeables.Routing;
using Routeables.Services;
using Routeables.Tests.Core;
using Routeables.Tests.Hosts;
using Routeables.Wpf;
using Routeables.Wpf.Services;
using Application = System.Windows.Application;
using Window = System.Windows.Window;

namespace Routeables.Tests.Wpf;

[Collection(WpfCollection.Name)]
public class WpfHostTests
{
    // The container of a test is the only one: the static locator is cleared and the container disposed, whatever the test did.
    private static async Task WithHostAsync(Func<IServiceProvider, Task> test)
    {
        IServiceProvider? provider = null;
        try
        {
            await WpfHarness.Run(() => provider = Application.Current.UseRouteables<FakeMainRouteables>());
            await test(provider!);
        }
        finally
        {
            ServiceLocator.Services = null;
            (provider as IDisposable)?.Dispose();
        }
    }

    [Fact]
    public Task UseRouteables_meets_the_host_contract() => WithHostAsync(provider =>
    {
        HostContract.ProvidesRouterAndInjection(provider);
        HostContract.MainViewModelIsOneInstance(provider);
        HostContract.ValueAttributesNeverComeFromTheContainer(provider);
        Assert.IsType<FakeMainRouteables>(provider.GetRequiredService<IMainRouteables>());
        return Task.CompletedTask;
    });

    [Fact]
    public Task The_services_of_the_host_are_the_ones_of_WPF() => WithHostAsync(provider =>
    {
        Assert.IsType<RouteableRequestThemeService>(provider.GetRequiredService<IRouteableRequestedThemeService>());
        Assert.IsType<RouteableDialogService>(provider.GetRequiredService<IRouteableDialogService>());
        Assert.IsType<RouteableContentDialogService>(provider.GetRequiredService<IRouteableContentDialogService>());
        Assert.IsType<RouteableTaskDialogService>(provider.GetRequiredService<IRouteableTaskDialogService>());
        Assert.IsType<RouteableNotificationService>(provider.GetRequiredService<IRouteableNotificationService>());
        Assert.IsType<DefaultMessageBoxService>(provider.GetRequiredService<IMessageBoxService>());
        Assert.IsType<RouteableDispatcher>(provider.GetRequiredService<IRouteableDispatcher>());
        return Task.CompletedTask;
    });

    [Fact]
    public Task GetRouteableServices_is_the_container_of_UseRouteables() => WithHostAsync(async provider =>
    {
        var found = await WpfHarness.Run(() => Application.Current.GetRouteableServices());
        Assert.Same(provider, found);
    });

    [Fact]
    public async Task GetRouteableServices_without_a_host_says_what_to_call()
    {
        ServiceLocator.Services = null;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => WpfHarness.Run(() => Application.Current.GetRouteableServices()));
        Assert.Contains("UseRouteables", error.Message);
    }

    [Fact]
    public async Task AttachRouteables_without_a_host_says_what_to_call()
    {
        ServiceLocator.Services = null;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => WpfHarness.Run(() => new Window().AttachRouteables()));
        Assert.Contains("UseRouteables", error.Message);
    }

    [Fact]
    public void UseRouteablesServices_twice_leaves_one_service_of_each_kind()
    {
        var services = new ServiceCollection();
        services.UseRouteablesServices().UseRouteablesServices();

        foreach (var type in new[] { typeof(IRouteableDialogService), typeof(IRouteableContentDialogService), typeof(IRouteableTaskDialogService), typeof(IRouteableNotificationService), typeof(IMessageBoxService) })
            Assert.Single(services, descriptor => descriptor.ServiceType == type);
    }

    [Fact]
    public Task The_last_window_to_close_takes_the_container_with_it() => WithHostAsync(async provider =>
    {
        var closed = false;
        var window = await WpfHarness.Run(() =>
        {
            var created = new Window { ShowActivated = false, Width = 200, Height = 100 }.AttachRouteables(onDispose: () => closed = true);
            created.Show();
            return created;
        });

        Assert.Same(window, RouteableWindows.Active);

        await WpfHarness.Run(window.Close);

        Assert.True(closed);
        Assert.Null(ServiceLocator.Services);
        Assert.Null(RouteableWindows.Active);
        Assert.Throws<ObjectDisposedException>(() => provider.GetService<IRouter>());
    });
}

[Collection(WpfCollection.Name)]
public class WpfDispatcherTests
{
    private readonly RouteableDispatcher _dispatcher = new();

    [Fact]
    public async Task The_dispatcher_is_on_the_thread_of_the_interface_only_on_that_thread()
    {
        Assert.False(_dispatcher.CheckAccess());
        Assert.True(await WpfHarness.Run(() => _dispatcher.CheckAccess()));
    }

    [Fact]
    public async Task Work_from_another_thread_runs_on_the_thread_of_the_interface()
    {
        var uiThread = await WpfHarness.Run(() => Environment.CurrentManagedThreadId);

        var ran = await Task.Run(() => _dispatcher.InvokeAsync(() => Environment.CurrentManagedThreadId), TestContext.Current.CancellationToken);

        Assert.Equal(uiThread, ran);
    }

    [Fact]
    public async Task Post_does_not_wait_and_runs_later_on_the_thread_of_the_interface()
    {
        var uiThread = await WpfHarness.Run(() => Environment.CurrentManagedThreadId);
        var ran = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        _dispatcher.Post(() => ran.SetResult(Environment.CurrentManagedThreadId));

        Assert.Equal(uiThread, await ran.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task An_exception_of_the_work_reaches_the_caller()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _dispatcher.InvokeAsync((Action)(() => throw new InvalidOperationException("boom"))));

        Assert.Equal("boom", error.Message);
    }
}
