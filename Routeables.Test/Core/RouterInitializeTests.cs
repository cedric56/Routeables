using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Test.Abstraction;
using static Routeables.Test.Abstraction.RouterTestKit;
using RouteableArgument = Routeables.Abstraction.RouteableArgument;

namespace Routeables.Tests.Core;

/// <summary><see cref="IInitializeAsync.InitializeAsync"/>: runs once per new routing, before the current page is left.</summary>
public class RouterInitializeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Initialize_RunsOnTheViewModelThenTheView_BeforeTheCurrentPageIsLeft()
    {
        var router = CreateRouter();
        var log = new CallLog();
        await GoToAsync(router, "A", log);
        log.Clear();

        await GoToAsync(router, "B", log);

        Assert.Equal(
        [
            "vmA:CanLeave", "viewA:CanLeave",
            "vmB:Init", "viewB:Init",
            "vmA:From", "viewA:From",
            "vmB:To", "viewB:To",
        ], log.Entries);
    }

    [Fact]
    public async Task Initialize_RunsBeforeTheFirstPageIsEntered()
    {
        var router = CreateRouter();
        var log = new CallLog();

        await GoToAsync(router, "A", log);

        Assert.Equal(["vmA:Init", "viewA:Init", "vmA:To", "viewA:To"], log.Entries);
    }

    [Fact]
    public async Task Initialize_ReceivesTheArgsOfTheRouting()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var args = RouteableArgs.Create().ForViewModel(new RouteableArgument(42, "id"));

        var page = await GoToAsync(router, "A", log, args);

        Assert.Same(args, page.ViewModel.InitializedWith);
        Assert.Same(args, page.View.InitializedWith);
    }

    [Fact]
    public async Task Initialize_SeesTheViewAlreadyBoundToItsViewModel()
    {
        var router = CreateRouter();
        var log = new CallLog();

        var page = await GoToAsync(router, "A", log);

        Assert.Same(page.ViewModel, page.View.ContextWhenInitialized);
    }

    [Fact]
    public async Task Initialize_IsOnlyCalledForNewRouteChanges()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var pages = await GoToAllAsync(router, log, "A", "B", "C", "D");
        log.Clear();

        await router.GoBackAsync(Ct);
        await router.ForwardAsync(Ct);
        await router.MoveToAsync(0, Ct);
        await router.MoveToAsync(3, Ct);
        await router.PopAsync(Ct);
        await router.GoBackToRootAsync(Ct);

        Assert.DoesNotContain(log.Entries, e => e.EndsWith(":Init"));
        Assert.All(pages, p => Assert.Equal(1, p.ViewModel.InitializeCount));
    }

    [Fact]
    public async Task Initialize_IsCalledOnce_WhenTheSameObjectIsTheViewAndTheViewModel()
    {
        var router = CreateRouter();
        var both = new RecordingView("both", new CallLog());

        await router.GoAsync(both, both, cancellationToken: Ct);

        Assert.Equal(1, both.InitializeCount);
    }

    [Fact]
    public async Task Initialize_IsCalledAgain_WhenTheSameInstanceIsRoutedToAgain()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var page = await GoToAsync(router, "A", log);

        await router.GoAsync(page.View, page.ViewModel, cancellationToken: Ct);   // same view and view model

        Assert.Equal(2, page.ViewModel.InitializeCount);
    }

    [Fact]
    public async Task Initialize_IsNotCalled_WhenTheCurrentPageRefusesToLeave()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var a = await GoToAsync(router, "A", log);
        a.ViewModel.CanLeave = false;
        var b = new RecordingViewModel("vmB", log);
        var viewB = new RecordingView("viewB", log);

        var result = await router.GoAsync(viewB, b, cancellationToken: Ct);

        Assert.True(result.Cancelled);
        Assert.Equal(0, b.InitializeCount);
        Assert.Equal(0, viewB.InitializeCount);
        Assert.Null(viewB.Context);                                  // never bound
    }

    [Fact]
    public async Task AFailingInitialize_KeepsTheRouterUnchanged_AndTheCurrentPageIsNotLeft()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var a = await GoToAsync(router, "A", log);
        log.Clear();
        var failing = new RecordingViewModel("vmB", log) { InitializeFails = new InvalidOperationException("init failed") };
        var viewB = new RecordingView("viewB", log);

        await Assert.ThrowsAsync<InvalidOperationException>(() => router.GoAsync(viewB, failing, cancellationToken: Ct));

        Assert.Same(a.ViewModel, router.CurrentViewModel);
        Assert.Equal(1, router.History.Count);
        Assert.Null(viewB.Context);                                  // the binding was undone
        Assert.Equal(["vmA:CanLeave", "viewA:CanLeave", "vmB:Init"], log.Entries);   // A was never left
    }

    [Fact]
    public async Task AFailingInitialize_ReleasesTheGate()
    {
        var router = CreateRouter();
        var log = new CallLog();
        await GoToAsync(router, "A", log);
        var failing = new RecordingViewModel("vmB", log) { InitializeFails = new InvalidOperationException("init failed") };
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.GoAsync(new RecordingView("viewB", log), failing, cancellationToken: Ct));

        var c = await GoToAsync(router, "C", log);                   // would hang or throw if the gate were still held

        Assert.Same(c.ViewModel, router.CurrentViewModel);
        Assert.Equal(2, router.History.Count);
    }
}
