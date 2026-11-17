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
}
