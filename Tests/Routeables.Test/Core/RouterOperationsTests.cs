using Routeables.Abstraction;
using Routeables.Routing;
using Routeables.Test.Abstraction;
using static Routeables.Test.Abstraction.RouterTestKit;

namespace Routeables.Tests.Core;

/// <summary>PopAsync, BackToRootAsync and MoveToAsync, through the real <see cref="Router"/>.</summary>
public class RouterOperationsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ------------------------------------------------------------------ PopAsync

    [Fact]
    public async Task PopAsync_ReturnsCancelled_WhenThereIsNoPreviousEntry()
    {
        var router = CreateRouter();
        var log = new CallLog();

        Assert.True((await router.PopAsync(Ct)).Cancelled);        // empty history

        await GoToAsync(router, "A", log);
        Assert.True((await router.PopAsync(Ct)).Cancelled);        // a single entry: nothing to go back to
        Assert.Equal(1, router.History.Count);
    }

    [Fact]
    public async Task PopAsync_GoesBack_AndDiscardsTheEntryItLeft()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var pages = await GoToAllAsync(router, log, "A", "B", "C");

        var result = await router.PopAsync(Ct);

        Assert.True(result.Success);
        Assert.Same(pages[1].ViewModel, router.CurrentViewModel);
        Assert.Same(pages[1].View, router.CurrentView);
        Assert.Equal(2, router.History.Count);
        Assert.True(router.CanGoBack);
        Assert.False(router.CanGoForward);                           // unlike BackAsync, nothing to go forward to
    }

    [Fact]
    public async Task PopAsync_AlsoDiscardsTheForwardEntries()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var pages = await GoToAllAsync(router, log, "A", "B", "C");
        await router.GoBackAsync(Ct);                                  // current = B, forward = C

        await router.PopAsync(Ct);

        Assert.Same(pages[0].ViewModel, router.CurrentViewModel);
        Assert.Equal(1, router.History.Count);
        Assert.False(router.CanGoBack);
        Assert.False(router.CanGoForward);
    }

    [Fact]
    public async Task PopAsync_IsCancelled_WhenTheCurrentPageRefuses()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var pages = await GoToAllAsync(router, log, "A", "B");
        pages[1].ViewModel.CanLeave = false;
        log.Clear();

        var result = await router.PopAsync(Ct);

        Assert.True(result.Cancelled);
        Assert.Same(pages[1].ViewModel, router.CurrentViewModel);
        Assert.Equal(2, router.History.Count);
        Assert.Empty(Destroyed(log));
    }

    [Fact]
    public async Task PopAsync_KeepsTheHistory_WhenTheTargetFailsToEnter()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var pages = await GoToAllAsync(router, log, "A", "B");
        pages[0].ViewModel.EnterFails = new InvalidOperationException("enter failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() => router.PopAsync(Ct));

        Assert.Same(pages[1].ViewModel, router.CurrentViewModel);
        Assert.Equal(2, router.History.Count);
        Assert.Empty(Destroyed(log));
    }

    // ------------------------------------------------------------ BackToRootAsync

    [Fact]
    public async Task BackToRootAsync_ReturnsCancelled_WhenAlreadyAtTheRoot()
    {
        var router = CreateRouter();
        var log = new CallLog();

        Assert.True((await router.GoBackToRootAsync(Ct)).Cancelled);   // empty

        await GoToAsync(router, "A", log);
        Assert.True((await router.GoBackToRootAsync(Ct)).Cancelled);   // only the root
    }

    [Fact]
    public async Task BackToRootAsync_GoesToTheFirstEntry_AndDiscardsTheOthers()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var pages = await GoToAllAsync(router, log, "A", "B", "C", "D");

        var result = await router.GoBackToRootAsync(Ct);

        Assert.True(result.Success);
        Assert.Same(pages[0].ViewModel, router.CurrentViewModel);
        Assert.Same(pages[0].View, router.CurrentView);
        Assert.Equal(1, router.History.Count);
        Assert.False(router.CanGoBack);
        Assert.False(router.CanGoForward);
    }

    [Fact]
    public async Task BackToRootAsync_IsCancelled_WhenTheCurrentPageRefuses()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var pages = await GoToAllAsync(router, log, "A", "B", "C");
        pages[2].ViewModel.CanLeave = false;
        log.Clear();

        var result = await router.GoBackToRootAsync(Ct);

        Assert.True(result.Cancelled);
        Assert.Equal(3, router.History.Count);
        Assert.Same(pages[2].ViewModel, router.CurrentViewModel);
        Assert.Empty(Destroyed(log));
    }

    // -------------------------------------------------------------- MoveToAsync

    [Fact]
    public async Task MoveToAsync_JumpsToTheEntry_AndKeepsEveryEntry()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var pages = await GoToAllAsync(router, log, "A", "B", "C");

        Assert.True((await router.MoveToAsync(0, Ct)).Success);

        Assert.Same(pages[0].ViewModel, router.CurrentViewModel);
        Assert.Equal(3, router.History.Count);
        Assert.False(router.CanGoBack);
        Assert.True(router.CanGoForward);

        Assert.True((await router.MoveToAsync(2, Ct)).Success);

        Assert.Same(pages[2].ViewModel, router.CurrentViewModel);
        Assert.Equal(2, router.History.CurrentIndex);
        Assert.True(router.CanGoBack);
        Assert.False(router.CanGoForward);
    }

    [Fact]
    public async Task MoveToAsync_RunsTheLifecycleOnlyOnTheCurrentPageAndTheTarget_AndDestroysNothing()
    {
        var router = CreateRouter();
        var log = new CallLog();
        await GoToAllAsync(router, log, "A", "B", "C");
        log.Clear();

        await router.MoveToAsync(0, Ct);

        Assert.Equal(
        [
            "vmC:CanLeave", "viewC:CanLeave",
            "vmC:From", "viewC:From",
            "vmA:To", "viewA:To",
        ], log.Entries);                                             // no Init (not a new routing), no Destroy
    }

    [Fact]
    public async Task MoveToAsync_ReturnsCancelled_ForAnInvalidOrCurrentIndex()
    {
        var router = CreateRouter();
        var log = new CallLog();
        await GoToAllAsync(router, log, "A", "B", "C");
        log.Clear();

        foreach (var index in new[] { -1, 3, 99, 2 })                // 2 is the current entry
            Assert.True((await router.MoveToAsync(index, Ct)).Cancelled);

        Assert.Equal(2, router.History.CurrentIndex);
        Assert.Empty(log.Entries);                                   // not even a guard was consulted
    }

    [Fact]
    public async Task MoveToAsync_IsCancelled_WhenTheCurrentPageRefuses()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var pages = await GoToAllAsync(router, log, "A", "B");
        pages[1].ViewModel.CanLeave = false;

        var result = await router.MoveToAsync(0, Ct);

        Assert.True(result.Cancelled);
        Assert.Equal(1, router.History.CurrentIndex);
    }

    [Fact]
    public async Task MoveToAsync_KeepsTheHistory_WhenTheTargetFailsToEnter()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var pages = await GoToAllAsync(router, log, "A", "B", "C");
        pages[0].ViewModel.EnterFails = new InvalidOperationException("enter failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() => router.MoveToAsync(0, Ct));

        Assert.Equal(2, router.History.CurrentIndex);
        Assert.Equal(3, router.History.Count);
    }

    // ------------------------------------------------------------------- shared

    [Fact]
    public async Task TheRouter_StaysUsable_AfterAFailedOrCancelledOperation()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var pages = await GoToAllAsync(router, log, "A", "B");

        pages[0].ViewModel.EnterFails = new InvalidOperationException("enter failed");
        await Assert.ThrowsAsync<InvalidOperationException>(() => router.PopAsync(Ct));
        pages[0].ViewModel.EnterFails = null;

        Assert.True((await router.PopAsync(Ct)).Success);           // the gate was released
        Assert.Same(pages[0].ViewModel, router.CurrentViewModel);
    }
}
