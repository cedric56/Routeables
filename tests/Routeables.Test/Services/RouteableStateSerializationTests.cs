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
    public void Two_snapshots_whose_keys_differ_by_case_are_never_equal_whatever_the_comparer_of_their_values()
    {
        var sensitive = new RouteableSnapshot("/p", new Dictionary<string, string> { ["A"] = "1" });
        var insensitive = new RouteableSnapshot("/p", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["a"] = "1" });

        Assert.False(sensitive.Equals(insensitive));
        Assert.False(insensitive.Equals(sensitive));
    }

    [Fact]
    public void Two_snapshots_with_the_same_keys_are_equal_both_ways_and_have_the_same_hash_whatever_the_comparer()
    {
        var sensitive = new RouteableSnapshot("/p", new Dictionary<string, string> { ["a"] = "1" });
        var insensitive = new RouteableSnapshot("/p", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["a"] = "1" });

        Assert.True(sensitive.Equals(insensitive));
        Assert.True(insensitive.Equals(sensitive));
        Assert.Equal(sensitive.GetHashCode(), insensitive.GetHashCode());
    }

    [Fact]
    public void Windows_line_ends_do_not_stay_on_the_route_or_the_last_value()
    {
        var back = RouteableState.DeserializeSnapshots("/page|key=value\r\n/other\r\n\r\n");

        Assert.Equal(2, back.Count);
        Assert.Equal("/page", back[0].Route);
        Assert.Equal("value", back[0].State["key"]);
        Assert.Equal("/other", back[1].Route);
    }

    [Fact]
    public void The_last_pages_are_kept_in_order_when_the_limit_applies_to_a_text_with_empty_lines_and_Windows_line_ends()
    {
        var pages = Enumerable.Range(0, RouteableState.MaxSnapshots + 5).Select(i => "/page" + i);
        var text = "\n\r\n" + string.Join("\r\n\n", pages) + "\r\n\n";

        var back = RouteableState.DeserializeSnapshots(text);

        Assert.Equal(RouteableState.MaxSnapshots, back.Count);
        Assert.Equal("/page5", back[0].Route);
        Assert.Equal("/page" + (RouteableState.MaxSnapshots + 4), back[^1].Route);
    }

    [Theory]
    [InlineData("b", "True", true)]
    [InlineData("b", "false", false)]
    [InlineData("g", "0f8fad5b-d9cb-469f-a165-70867728950e", null)]
    public void A_bool_and_a_Guid_are_read_as_they_are_written(string tag, string text, bool? expected)
    {
        var value = RouteableState.Parse(tag, text);

        if (tag == "b")
            Assert.Equal((object?)expected, value);
        else
            Assert.Equal(Guid.Parse(text), value);
    }

    [Theory]
    [InlineData("b", " true")]
    [InlineData("b", "true ")]
    [InlineData("b", "true\0")]
    [InlineData("b", "yes")]
    [InlineData("g", "{0f8fad5b-d9cb-469f-a165-70867728950e}")]
    [InlineData("g", "0f8fad5bd9cb469fa16570867728950e")]
    [InlineData("g", " 0f8fad5b-d9cb-469f-a165-70867728950e")]
    public void A_bool_or_a_Guid_in_another_spelling_is_not_read(string tag, string text)
    {
        Assert.Null(RouteableState.Parse(tag, text));
    }

    [Fact]
    public void A_string_with_more_pages_than_the_limit_gives_the_last_ones()
    {
        var text = string.Join('\n', Enumerable.Range(0, RouteableState.MaxSnapshots + 50).Select(i => "/page" + i));

        var back = RouteableState.DeserializeSnapshots(text);

        Assert.Equal(RouteableState.MaxSnapshots, back.Count);
        Assert.Equal("/page50", back[0].Route);
        Assert.Equal("/page" + (RouteableState.MaxSnapshots + 49), back[^1].Route);
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
