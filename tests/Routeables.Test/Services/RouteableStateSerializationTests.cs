using Routeables.Services;
namespace Routeables.Tests.Services;

/// <summary>The text that keeps a history between two runs: what is written is read back, and what was edited by hand is read without failing.</summary>
public class RouteableStateSerializationTests
{
    [Fact]
    public void Routes_with_separators_and_accents_are_read_back_as_they_were()
    {
        string[] routes = ["/a b/é?x=1&y=2|3", "/line\nbreak", "/100%"];

        Assert.Equal(routes, RouteableState.Deserialize(RouteableState.Serialize(routes)));
    }

    [Fact]
    public void Values_are_read_back_with_their_page()
    {
        var snapshots = new[]
        {
            new RouteableSnapshot("/first", new Dictionary<string, string> { ["name"] = "a|b=c", ["empty"] = "" }),
            new RouteableSnapshot("/second"),
        };

        var back = RouteableState.DeserializeSnapshots(RouteableState.Serialize(snapshots));

        Assert.Equal(snapshots, back);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Nothing_gives_no_page(string? text)
    {
        Assert.Empty(RouteableState.DeserializeSnapshots(text));
        Assert.Empty(RouteableState.Deserialize(text));
    }

    [Fact]
    public void A_part_without_a_name_is_left_out_and_the_route_is_kept()
    {
        var back = RouteableState.DeserializeSnapshots("/page|=orphan|noequal|kept=yes\n\n/other");

        Assert.Equal(2, back.Count);
        Assert.Equal("/page", back[0].Route);
        Assert.Equal(new Dictionary<string, string> { ["kept"] = "yes" }, back[0].State);
        Assert.Equal("/other", back[1].Route);
        Assert.Empty(back[1].State);
    }

    [Fact]
    public void Two_snapshots_are_equal_when_their_route_and_values_are_equal()
    {
        var one = new RouteableSnapshot("/a", new Dictionary<string, string> { ["k"] = "v" });

        Assert.Equal(one, new RouteableSnapshot("/a", new Dictionary<string, string> { ["k"] = "v" }));
        Assert.NotEqual(one, new RouteableSnapshot("/a", new Dictionary<string, string> { ["k"] = "w" }));
        Assert.NotEqual(one, new RouteableSnapshot("/a", new Dictionary<string, string> { ["j"] = "v" }));
        Assert.NotEqual(one, new RouteableSnapshot("/b", new Dictionary<string, string> { ["k"] = "v" }));
        Assert.NotEqual(one, new RouteableSnapshot("/a"));
        Assert.Equal(one.GetHashCode(), new RouteableSnapshot("/a", new Dictionary<string, string> { ["k"] = "v" }).GetHashCode());
    }

    [Fact]
    public void Missing_arguments_are_refused()
    {
        Assert.Throws<ArgumentNullException>(() => RouteableState.Serialize((IEnumerable<string>)null!));
        Assert.Throws<ArgumentNullException>(() => RouteableState.Serialize((IEnumerable<RouteableSnapshot>)null!));
        Assert.Throws<ArgumentNullException>(() => RouteableState.Capture(null!));
        Assert.Throws<ArgumentNullException>(() => RouteableState.CaptureSnapshots(null!));
    }
}
