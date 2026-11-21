using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Services;
using Routeables.Tests.Pages;

namespace Routeables.Tests.Services;

/// <summary>The values of a page that are kept beside its address (<c>WithValue</c>): what is written, what is read back, and what is left out.</summary>
public class RouteableStateValuesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<FakeViewFor, SecondViewModel>(route: "/home");
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Only_the_named_values_that_can_be_written_as_text_are_captured()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var args = RouteableArgs.Create()
            .WithValue("count", 5)
            .WithValue("title", "Home")
            .WithValue("object", new object())
            .WithValue("", "no name");
        await router.GoToAsync("/home", args, cancellationToken: Ct);

        var snapshot = Assert.Single(RouteableState.CaptureSnapshots(router));

        Assert.Equal("/home", snapshot.Route);
        Assert.Equal("5", snapshot.State[RouteableState.ValuePrefix + "i:count"]);
        Assert.Equal("Home", snapshot.State[RouteableState.ValuePrefix + "s:title"]);
        Assert.DoesNotContain(snapshot.State.Keys, key => key.EndsWith(":object", StringComparison.Ordinal));
        Assert.Equal(2, snapshot.State.Keys.Count(key => key.StartsWith(RouteableState.ValuePrefix, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_page_shown_without_a_route_is_not_captured()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoAsync(new FakeViewFor(), new FakeViewModel(), cancellationToken: Ct);

        Assert.Empty(RouteableState.CaptureSnapshots(router));
    }

    [Fact]
    public async Task Entries_of_the_state_that_are_not_values_or_cannot_be_read_are_ignored_and_the_others_come_back()
    {
        await using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var state = new Dictionary<string, string>
        {
            ["junk"] = "x",
            [RouteableState.ValuePrefix + "novalue"] = "y",
            [RouteableState.ValuePrefix + "i:bad"] = "not a number",
            [RouteableState.ValuePrefix + "i:ok"] = "7",
        };

        var restored = await RouteableState.RestoreSnapshotsAsync(router, [new RouteableSnapshot("/home", state)], Ct);

        Assert.True(restored);
        Assert.IsType<SecondViewModel>(router.CurrentViewModel);
        var again = Assert.Single(RouteableState.CaptureSnapshots(router));
        Assert.Equal("7", again.State[RouteableState.ValuePrefix + "i:ok"]);
        Assert.DoesNotContain(again.State.Keys, key => key.EndsWith(":bad", StringComparison.Ordinal));
    }
}

/// <summary>The arguments of a page reached again keep what they are given, and give themselves back to chain.</summary>
public class RestoredArgsTests
{
    [Fact]
    public void The_entries_given_are_kept_by_destination_and_the_arguments_are_returned_to_chain()
    {
        var args = new RestoredArgs();
        var one = new RouteableArgument(1, "one");
        var two = new RouteableArgument(2, "two");
        var three = new RouteableArgument(3, "three");

        Assert.Same(args, args.ForViewModel(one));
        Assert.Same(args, args.ForView(two));
        Assert.Same(args, args.ForValues(three));
        Assert.Same(args, args.WithRoute("/home"));

        Assert.Equal(new[] { one }, args.ViewModelParameters);
        Assert.Equal(new[] { two }, args.ViewParameters);
        Assert.Equal(new[] { three }, args.Values);
        Assert.Equal("/home", args.Route);
    }
}
