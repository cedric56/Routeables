using Routeables.Abstraction.Registrations;

namespace Routeables.Tests.Abstraction;

/// <summary>The table of routes is the one of the whole application: what it hands out cannot be used to change it.</summary>
public class RouteableTableTests
{
    [Fact]
    public void The_entries_are_read_only_and_follow_the_routes_that_are_added()
    {
        var table = new RouteableTable();
        table.Add("/rt-first", typeof(string));
        var before = table.Entries;

        table.Add("/rt-second", null);
        table.Add("/RT-first/", typeof(int));   // the same path: not added twice

        Assert.False(before is (string, Type?)[]);
        Assert.True(((System.Collections.Generic.IList<(string Template, Type? ViewModelType)>)before).IsReadOnly);
        Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<(string Template, Type? ViewModelType)>)before).Add(("/rt-hack", null)));
        Assert.Single(before);                   // a list read before an add does not change under the reader
        Assert.Equal(2, table.Entries.Count);
        Assert.Equal("/rt-second", table.Entries[1].Template);
    }

    [Fact]
    public void A_route_registered_again_for_another_view_model_follows_the_last_one_like_the_container()
    {
        var table = new RouteableTable();

        table.Add("/rt-x", typeof(string));
        table.Add("/rt-x", null);               // no view model: nothing changes
        Assert.Equal(typeof(string), table.Entries[0].ViewModelType);

        table.Add("/rt-x", typeof(int));        // another view model: the last one, as the container keeps the last view

        var entry = Assert.Single(table.Entries);
        Assert.Equal(typeof(int), entry.ViewModelType);
    }

    [Fact]
    public void Two_routes_that_differ_only_by_their_query_are_both_kept()
    {
        var table = new RouteableTable();

        table.Add("/rt-search", typeof(string));
        table.Add("/rt-search?mode=grid", typeof(int));
        table.Add("/RT-search/?mode=grid", typeof(long));    // the same route as the one above: not added twice

        Assert.Equal(["/rt-search", "/rt-search?mode=grid"], table.Entries.Select(e => e.Template));
    }
}
