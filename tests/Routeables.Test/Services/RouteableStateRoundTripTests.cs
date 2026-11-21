using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Routeables.Abstraction.History;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Abstraction.States;
using Routeables.Routing;
using Routeables.Services;
using Routeables.Tests.Pages;

namespace Routeables.Tests.Services;

/// <summary>Capturing a history and restoring it in another run: the values of the pages, the pages that cannot be reached, and what is left of the history then.</summary>
public class RouteableStateRoundTripTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public sealed class KeeperView : IRouteableFor { public object? Context { get; set; } }

    public sealed class KeeperVm : IRouteableState
    {
        public string Draft { get; set; } = "";
        public Dictionary<string, string>? Loaded { get; private set; }

        public void SaveState(IDictionary<string, string> state) => state["draft"] = Draft;

        public void LoadState(IReadOnlyDictionary<string, string> state) => Loaded = new Dictionary<string, string>(state);
    }

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<FakeViewFor, FakeViewModel>(route: "/home");
        services.RegisterRoute<SecondViewFor, SecondViewModel>(route: "/second");
        services.RegisterRoute<KeeperView, KeeperVm>(route: "/keeper");
        return services.BuildServiceProvider();
    }

    private static object? ValueOf(IRouter router, string name)
        => router.History.Current!.Args.Values.Where(v => v.name == name).Select(v => v.value).SingleOrDefault();

    [Fact]
    public async Task The_values_a_page_was_reached_with_come_back_with_their_type()
    {
        var when = new DateTime(2026, 11, 21, 9, 30, 0, DateTimeKind.Utc);
        var moment = new DateTimeOffset(2026, 11, 21, 9, 30, 0, TimeSpan.FromHours(1));
        var id = Guid.NewGuid();
        await using var first = Build();
        var before = first.GetRequiredService<IRouter>();
        await before.GoToAsync("/home", RouteableArgs.Create()
            .WithValue("text", "a|b=c é")
            .WithValue("int", 5).WithValue("long", 6L).WithValue("flag", true)
            .WithValue("double", 1.5).WithValue("decimal", 2.25m)
            .WithValue("guid", id).WithValue("time", when).WithValue("moment", moment)
            .WithValue("object", new object()),            // not a kind that is kept
            cancellationToken: Ct);

        var kept = RouteableState.Serialize(RouteableState.CaptureSnapshots(before));
        await using var second = Build();
        var after = second.GetRequiredService<IRouter>();
        Assert.True(await RouteableState.RestoreSnapshotsAsync(after, RouteableState.DeserializeSnapshots(kept), Ct));

        Assert.Equal("a|b=c é", ValueOf(after, "text"));
        Assert.Equal(5, ValueOf(after, "int"));
        Assert.Equal(6L, ValueOf(after, "long"));
        Assert.Equal(true, ValueOf(after, "flag"));
        Assert.Equal(1.5, ValueOf(after, "double"));
        Assert.Equal(2.25m, ValueOf(after, "decimal"));
        Assert.Equal(id, ValueOf(after, "guid"));
        Assert.Equal(when, ValueOf(after, "time"));
        Assert.Equal(moment, ValueOf(after, "moment"));
        Assert.Null(ValueOf(after, "object"));
    }

    [Fact]
    public async Task A_value_that_is_in_the_address_is_not_kept_twice()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/home?page=2", RouteableArgs.Create().WithValue("page", 2).WithValue("other", 1), cancellationToken: Ct);

        var snapshot = Assert.Single(RouteableState.CaptureSnapshots(router));

        Assert.DoesNotContain(snapshot.State.Keys, key => key.EndsWith(":page", StringComparison.Ordinal));
        Assert.Contains(snapshot.State.Keys, key => key.EndsWith(":other", StringComparison.Ordinal));
    }

    [Fact]
    public async Task What_a_view_model_saves_is_given_back_without_the_values_of_the_route()
    {
        await using var first = Build();
        var before = first.GetRequiredService<IRouter>();
        await before.GoToAsync("/keeper", RouteableArgs.Create().WithValue("count", 3), cancellationToken: Ct);
        ((KeeperVm)before.CurrentViewModel!).Draft = "half a sentence";

        var snapshots = RouteableState.CaptureSnapshots(before);
        await using var second = Build();
        var after = second.GetRequiredService<IRouter>();
        Assert.True(await RouteableState.RestoreSnapshotsAsync(after, snapshots, Ct));

        var loaded = ((KeeperVm)after.CurrentViewModel!).Loaded;
        Assert.NotNull(loaded);
        Assert.Equal("half a sentence", loaded["draft"]);
        Assert.DoesNotContain(loaded.Keys, key => key.StartsWith(RouteableState.ValuePrefix, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Values_that_cannot_be_read_are_left_out_and_the_page_is_still_reached()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var state = new Dictionary<string, string>
        {
            ["value:i:count"] = "not a number",
            ["value:zz:kind"] = "x",                    // a kind nobody wrote
            ["value:nocolon"] = "x",
            ["value::empty"] = "x",
            ["value:s:name"] = "kept",
        };

        Assert.True(await RouteableState.RestoreSnapshotsAsync(router, [new RouteableSnapshot("/home", state)], Ct));

        Assert.Equal("kept", ValueOf(router, "name"));
        Assert.Single(router.History.Current!.Args.Values);
    }

    [Fact]
    public async Task When_the_first_page_cannot_be_reached_the_page_that_was_shown_comes_back()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/second", cancellationToken: Ct);

        var restored = await RouteableState.RestoreAsync(router, ["/removed-page", "/home"], Ct);

        Assert.False(restored);
        Assert.IsType<SecondViewModel>(router.CurrentViewModel);
        Assert.Equal(1, router.History.Count);
    }

    [Fact]
    public async Task When_a_later_page_cannot_be_reached_the_pages_before_it_stay()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        var restored = await RouteableState.RestoreAsync(router, ["/home", "/second", "/removed-page", "/home"], Ct);

        Assert.False(restored);
        Assert.Equal(2, router.History.Count);
        Assert.IsType<SecondViewModel>(router.CurrentViewModel);
    }

    [Fact]
    public async Task Nothing_to_restore_leaves_the_history_and_says_it_succeeded()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/home", cancellationToken: Ct);

        Assert.True(await RouteableState.RestoreAsync(router, [], Ct));
        Assert.True(await RouteableState.RestoreAsync(router, ["", "  "], Ct));

        Assert.Equal(1, router.History.Count);
    }

    [Fact]
    public async Task The_pages_that_can_be_reached_with_Forward_are_not_captured()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/home", cancellationToken: Ct);
        await router.GoToAsync("/second", cancellationToken: Ct);
        await router.GoBackAsync(Ct);

        Assert.Equal(["/home"], RouteableState.Capture(router));
        Assert.Equal(["/home"], RouteableState.CaptureSnapshots(router).Select(s => s.Route));
    }

    [Fact]
    public async Task Missing_arguments_are_refused()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();

        await Assert.ThrowsAsync<ArgumentNullException>(() => RouteableState.RestoreAsync(null!, [], Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() => RouteableState.RestoreAsync(router, null!, Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() => RouteableState.RestoreSnapshotsAsync(null!, [], Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() => RouteableState.RestoreSnapshotsAsync(router, null!, Ct));
    }

    [Fact]
    public async Task A_route_that_throws_during_the_restoration_is_given_back_after_the_page_that_was_shown_is_tried_again()
    {
        var shown = Substitute.For<IHistoryEntry>();
        shown.Args.Returns(RouteableArgs.Create().WithRoute("/was"));
        var history = Substitute.For<IRouterHistory>();
        history.Current.Returns(shown);
        history.Count.Returns(0);                       // erased, and nothing is back
        var router = Substitute.For<IRouter>();
        router.History.Returns(history);
        router.GoToAsync(Arg.Any<string>(), Arg.Any<IRouteableArgs?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("the page failed"));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => RouteableState.RestoreAsync(router, ["/a"], Ct));

        // The failure of the restoration is the one the caller gets, even though the page that was shown could not be shown again either.
        Assert.Equal("the page failed", failure.Message);
        await router.Received(1).GoToAsync("/was", null, null, CancellationToken.None);
    }

    [Fact]
    public async Task A_page_that_has_no_address_is_not_captured()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/home", cancellationToken: Ct);
        Assert.True((await router.GoAsync(new SixthViewFor(), new ThirdViewModel(), cancellationToken: Ct)).Success);

        Assert.Equal(["/home"], RouteableState.Capture(router));
        Assert.Equal(["/home"], RouteableState.CaptureSnapshots(router).Select(snapshot => snapshot.Route));
    }
}
