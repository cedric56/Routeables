using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Routeables.Abstraction;
using Routeables.Abstraction.Routing;
using Routeables.Avalonia;
using Xunit;

namespace Routeables.Test.Avalonia;

/// <summary>
/// The system's Back request (the Android button, the mouse back button, Alt+Left) goes back one page of the default router, and is left to the
/// system on the first page.
/// </summary>
[Collection("Avalonia headless")]
public sealed class HeadlessBackRequestsTests(HeadlessApp app)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    // Starts from an empty history on pages A then B, with a window that hooks the Back requests.
    private Task RunAsync(Func<IServiceProvider, IRouter, Window, Task> test) => app.RunAsync(async () =>
    {
        var provider = app.Provider;
        var router = provider.GetRouter(null);
        await router.EraseHistoryAsync();

        var content = new RouteableRegion();
        var window = new Window { Width = 400, Height = 300, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        BackRequests.Attach(content, provider);
        try
        {
            await provider.GoToAsync("/a");
            await provider.GoToAsync("/b");
            await UntilAsync(() => router.CurrentViewModel is PageBViewModel);
            await test(provider, router, window);
        }
        finally
        {
            window.Close();
            await router.EraseHistoryAsync();
        }
    });

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.Add(Timeout);
        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition()) return;
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition was not met in 5 seconds.");
            await Task.Delay(10);
        }
    }

    [Fact]
    public Task The_Back_request_of_the_system_goes_back_one_page() => RunAsync(async (provider, router, window) =>
    {
        var request = new RoutedEventArgs(TopLevel.BackRequestedEvent);
        window.RaiseEvent(request);

        Assert.True(request.Handled);
        await UntilAsync(() => router.CurrentViewModel is PageAViewModel);
    });

    [Fact]
    public Task A_Back_request_on_the_first_page_is_left_to_the_system() => RunAsync(async (provider, router, window) =>
    {
        await router.GoBackAsync();
        await UntilAsync(() => router.CurrentViewModel is PageAViewModel);

        var request = new RoutedEventArgs(TopLevel.BackRequestedEvent);
        window.RaiseEvent(request);

        Assert.False(request.Handled);
        Assert.IsType<PageAViewModel>(router.CurrentViewModel);
    });

    [Fact]
    public Task A_Back_request_that_is_handled_already_is_left_alone() => RunAsync(async (provider, router, window) =>
    {
        var request = new RoutedEventArgs(TopLevel.BackRequestedEvent) { Handled = true };
        window.RaiseEvent(request);
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(50);

        Assert.IsType<PageBViewModel>(router.CurrentViewModel);
    });

    [Fact]
    public Task Alt_and_Left_go_back_and_Left_alone_does_not() => RunAsync(async (provider, router, window) =>
    {
        var left = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Left };
        window.RaiseEvent(left);
        Assert.False(left.Handled);

        var altLeft = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Left, KeyModifiers = KeyModifiers.Alt };
        window.RaiseEvent(altLeft);

        Assert.True(altLeft.Handled);
        await UntilAsync(() => router.CurrentViewModel is PageAViewModel);
    });

    [Fact]
    public Task The_back_button_of_the_mouse_goes_back() => RunAsync(async (provider, router, window) =>
    {
        window.MouseDown(new Point(10, 10), MouseButton.XButton1);
        window.MouseUp(new Point(10, 10), MouseButton.XButton1);

        await UntilAsync(() => router.CurrentViewModel is PageAViewModel);
    });

    [Fact]
    public Task A_view_that_is_not_shown_yet_hooks_its_window_when_it_is_attached() => app.RunAsync(async () =>
    {
        var provider = app.Provider;
        var router = provider.GetRouter(null);
        await router.EraseHistoryAsync();
        await provider.GoToAsync("/a");
        await provider.GoToAsync("/b");

        var content = new Border();
        BackRequests.Attach(content, provider);
        var window = new Window { Width = 400, Height = 300, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var request = new RoutedEventArgs(TopLevel.BackRequestedEvent);
            window.RaiseEvent(request);

            Assert.True(request.Handled);
            await UntilAsync(() => router.CurrentViewModel is PageAViewModel);
        }
        finally
        {
            window.Close();
            await router.EraseHistoryAsync();
        }
    });
}
