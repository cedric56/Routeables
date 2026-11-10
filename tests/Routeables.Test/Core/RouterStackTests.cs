using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Test.Abstraction;
using static Routeables.Test.Abstraction.RouterTestKit;
using Page = Routeables.Test.Abstraction.Page;

namespace Routeables.Tests.Core;

/// <summary>GoBackToAsync, GoBackAsync(steps) and GoToStackAsync.</summary>
public class RouterStackTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(IRouter router, List<Page> pages)> ThreePagesAsync()
    {
        var router = CreateRouter();
        var log = new CallLog();
        var pages = new List<Page>();
        foreach (var name in new[] { "A", "B", "C" })
            pages.Add(await GoToAsync(router, name, log, RouteableArgs.Create().WithRoute("/" + name.ToLowerInvariant())));
        return (router, pages);
    }

    [Fact]
    public async Task GoBackToAsync_Route_GoesToTheNearestEarlierEntry_WhateverTheCaseAndTheSlash()
    {
        var (router, pages) = await ThreePagesAsync();

        var result = await router.GoBackToAsync("A?x=1", Ct);

        Assert.True(result.Success);
        Assert.Same(pages[0].ViewModel, router.CurrentViewModel);
        Assert.Equal(3, router.History.Count);       // the later entries stay: it is a move, not a pop
        Assert.True(router.CanGoForward);
    }

    [Fact]
    public async Task GoBackToAsync_IsCancelled_WhenNoEarlierEntryMatches()
    {
        var (router, pages) = await ThreePagesAsync();

        Assert.True((await router.GoBackToAsync("/c", Ct)).Cancelled);   // the current page is not "earlier"
        Assert.True((await router.GoBackToAsync("/zzz", Ct)).Cancelled);
        Assert.Same(pages[2].ViewModel, router.CurrentViewModel);
    }

    [Fact]
    public async Task GoBackToAsync_Predicate_AndViewModelType()
    {
        var (router, pages) = await ThreePagesAsync();

        Assert.True((await router.GoBackToAsync(e => ReferenceEquals(e.ViewModel, pages[1].ViewModel), Ct)).Success);
        Assert.Same(pages[1].ViewModel, router.CurrentViewModel);

        Assert.True((await router.GoBackToAsync<RecordingViewModel>(Ct)).Success);   // nearest earlier one: A
        Assert.Same(pages[0].ViewModel, router.CurrentViewModel);
    }

    [Fact]
    public async Task GoBackAsync_Steps_JumpsInOneMove_AndCancelsWhenTooFar()
    {
        var (router, pages) = await ThreePagesAsync();

        Assert.True((await router.GoBackAsync(3, Ct)).Cancelled);
        Assert.Same(pages[2].ViewModel, router.CurrentViewModel);

        Assert.True((await router.GoBackAsync(2, Ct)).Success);
        Assert.Same(pages[0].ViewModel, router.CurrentViewModel);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => router.GoBackAsync(0, Ct));
    }

    [Fact]
    public async Task GoToStackAsync_StopsAtTheFirstFailure_AndAnEmptyListChangesNothing()
    {
        var router = CreateRouter();

        Assert.True((await router.GoToStackAsync([], Ct)).Cancelled);

        var result = await router.GoToStackAsync(["/not-registered", "/other"], Ct);

        Assert.False(result.Success);
        Assert.NotNull(result.Exception);
        Assert.Equal(0, router.History.Count);
    }

    // Switching tabs/regions goes through the router (RegionTabs routes the selected tab), so the pages told "entered" and
    // "left" on every switch, again when they come back: that is the "active / inactive" notification.
    [Fact]
    public async Task EveryMove_TellsThePageLeftAndThePageEntered_EvenWhenComingBack()
    {
        var router = CreateRouter();
        var log = new CallLog();
        await GoToAllAsync(router, log, "A", "B");
        log.Clear();

        await router.GoBackAsync(Ct);
        Assert.Contains("vmB:From", log.Entries);
        Assert.Contains("vmA:To", log.Entries);

        log.Clear();
        await router.ForwardAsync(Ct);
        Assert.Contains("vmA:From", log.Entries);
        Assert.Contains("vmB:To", log.Entries);
    }

    [Fact]
    public async Task GoToAsync_Qualifier_Dash_GoesBack()
    {
        var (router, pages) = await ThreePagesAsync();

        Assert.True((await router.GoToAsync("-",cancellationToken:TestContext.Current.CancellationToken)).Success);
        Assert.Same(pages[1].ViewModel, router.CurrentViewModel);

        Assert.True((await router.GoToAsync(" - ", cancellationToken: TestContext.Current.CancellationToken)).Success);          // spaces are ignored
        Assert.True((await router.GoToAsync("-", cancellationToken: TestContext.Current.CancellationToken)).Cancelled);          // first page: nothing to go back to
    }

    [Fact]
    public async Task GoToAsync_Qualifier_DashSlashRoute_GoesBackThenFails_WhenTheRouteIsUnknown()
    {
        var (router, pages) = await ThreePagesAsync();

        var result = await router.GoToAsync("-/not-registered", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);                                  // the route is unknown...
        Assert.Same(pages[1].ViewModel, router.CurrentViewModel);      // ...but the back part was done
    }
}
