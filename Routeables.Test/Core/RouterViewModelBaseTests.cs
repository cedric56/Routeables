using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Test.Abstraction;
using static Routeables.Test.Abstraction.RouterTestKit;

namespace Routeables.Tests.Core;

/// <summary>
/// <see cref="Routeables.Core.RouterViewModelBase.GoBackAsync"/> / <see cref="Routeables.Core.RouterViewModelBase.GoForwardAsync"/> on a real
/// <see cref="Router"/>. These used to pass the <see cref="RouteableResult"/> returned by BackAsync / ForwardAsync to
/// OnViewModelChanged, so the active view model was never found.
/// </summary>
public class RouterViewModelBaseTests
{
    [Fact]
    public async Task CanGoBack_And_CanGoForward_ReflectTheRouterState()
    {
        var router = CreateRouter();
        var sut = new TestRouterViewModel(router);
        var log = new CallLog();

        Assert.False(sut.CanGoBack());
        Assert.False(sut.CanGoForward());

        await GoToAllAsync(router, log, "A", "B");
        Assert.True(sut.CanGoBack());
        Assert.False(sut.CanGoForward());

        await router.GoBackAsync(TestContext.Current.CancellationToken);
        Assert.False(sut.CanGoBack());
        Assert.True(sut.CanGoForward());
    }

    [Fact]
    public async Task GoBack_NotifiesOnViewModelChanged_WithTheViewModelThatIsNowCurrent()
    {
        var router = CreateRouter();
        var sut = new TestRouterViewModel(router);
        var pages = await GoToAllAsync(router, new CallLog(), "A", "B");

        await sut.GoBackAsync();

        Assert.Same(pages[0].ViewModel, router.CurrentViewModel);
        Assert.Same(pages[0].ViewModel, sut.LastViewModelChanged);   // the view model, not the RouteableResult
        Assert.False(sut.LastViewModelChanged is RouteableResult);
        Assert.Equal(1, sut.RaiseCanExecutesChangedCallCount);
    }

    [Fact]
    public async Task GoBack_DoesNothing_WhenThereIsNothingToGoBackTo()
    {
        var router = CreateRouter();
        var sut = new TestRouterViewModel(router);
        await GoToAsync(router, "A", new CallLog());

        await sut.GoBackAsync();

        Assert.Null(sut.LastViewModelChanged);
        Assert.Equal(0, sut.RaiseCanExecutesChangedCallCount);
    }

    [Fact]
    public async Task GoBack_DoesNotNotify_WhenAGuardCancelsTheRouting()
    {
        var router = CreateRouter();
        var sut = new TestRouterViewModel(router);
        var pages = await GoToAllAsync(router, new CallLog(), "A", "B");
        pages[1].ViewModel.CanLeave = false;

        await sut.GoBackAsync();

        Assert.Same(pages[1].ViewModel, router.CurrentViewModel);    // still on B
        Assert.Null(sut.LastViewModelChanged);
        Assert.Equal(0, sut.RaiseCanExecutesChangedCallCount);
    }

    [Fact]
    public async Task GoBack_DoesNotNotify_WhenTheRoutingThrows()
    {
        var router = CreateRouter();
        var sut = new TestRouterViewModel(router);
        var pages = await GoToAllAsync(router, new CallLog(), "A", "B");
        pages[0].ViewModel.EnterFails = new InvalidOperationException("enter failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GoBackAsync());

        Assert.Null(sut.LastViewModelChanged);
        Assert.Equal(0, sut.RaiseCanExecutesChangedCallCount);
    }

    [Fact]
    public async Task GoForward_NotifiesOnViewModelChanged_WithTheViewModelThatIsNowCurrent()
    {
        var router = CreateRouter();
        var sut = new TestRouterViewModel(router);
        var pages = await GoToAllAsync(router, new CallLog(), "A", "B");
        await router.GoBackAsync(TestContext.Current.CancellationToken);

        await sut.GoForwardAsync();

        Assert.Same(pages[1].ViewModel, router.CurrentViewModel);
        Assert.Same(pages[1].ViewModel, sut.LastViewModelChanged);
        Assert.False(sut.LastViewModelChanged is RouteableResult);
        Assert.Equal(1, sut.RaiseCanExecutesChangedCallCount);
    }

    [Fact]
    public async Task GoForward_DoesNothing_WhenThereIsNothingToGoForwardTo()
    {
        var router = CreateRouter();
        var sut = new TestRouterViewModel(router);
        await GoToAllAsync(router, new CallLog(), "A", "B");

        await sut.GoForwardAsync();

        Assert.Null(sut.LastViewModelChanged);
        Assert.Equal(0, sut.RaiseCanExecutesChangedCallCount);
    }

    [Fact]
    public async Task GoForward_DoesNotNotify_WhenAGuardCancelsTheRouting()
    {
        var router = CreateRouter();
        var sut = new TestRouterViewModel(router);
        var pages = await GoToAllAsync(router, new CallLog(), "A", "B");
        await router.GoBackAsync(TestContext.Current.CancellationToken);
        pages[0].ViewModel.CanLeave = false;

        await sut.GoForwardAsync();

        Assert.Same(pages[0].ViewModel, router.CurrentViewModel);
        Assert.Null(sut.LastViewModelChanged);
        Assert.Equal(0, sut.RaiseCanExecutesChangedCallCount);
    }
}
