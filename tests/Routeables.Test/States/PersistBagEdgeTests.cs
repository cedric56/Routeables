using Routeables.Abstraction.States;
using System.Collections.ObjectModel;

namespace Routeables.Tests.States;

/// <summary>The edges of <see cref="PersistBag"/>: the other side of a bag, the failures it swallows, and the types written as text.</summary>
public class PersistBagEdgeTests
{
    private sealed class Throwing<T> : IPersistConverter<T>
    {
        public string? Write(T value) => throw new InvalidOperationException("no");
        public bool TryRead(string text, out T value) => throw new InvalidOperationException("no");
    }

    private sealed class Vm
    {
        public int Count { get; set; } = 3;
    }

    [Fact]
    public void A_bag_made_to_read_writes_nothing()
    {
        var bag = new PersistBag((IReadOnlyDictionary<string, string>)new Dictionary<string, string>());

        Assert.False(bag.Set("Count", 1));
    }

    [Fact]
    public void A_bag_made_to_write_reads_nothing()
    {
        var bag = new PersistBag((IDictionary<string, string>)new Dictionary<string, string> { ["persist:Count"] = "v1" });

        Assert.False(bag.TryGet<int>("Count", out var value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void A_dictionary_that_refuses_writes_leaves_the_value_out_without_throwing()
    {
        var refusing = new ReadOnlyDictionary<string, string>(new Dictionary<string, string> { ["persist:Count"] = "v9" });
        var bag = new PersistBag((IDictionary<string, string>)refusing);

        Assert.False(bag.Set("Count", 1));
    }

    [Fact]
    public void A_converter_that_throws_leaves_the_value_out_and_removes_what_was_there()
    {
        var into = new Dictionary<string, string> { ["persist:Count"] = "v9" };
        var bag = new PersistBag((IDictionary<string, string>)into);

        Assert.False(bag.Set("Count", 1, new Throwing<int>()));
        Assert.Empty(into);
    }

    [Fact]
    public void A_converter_that_throws_while_reading_leaves_the_property_alone()
    {
        var from = new Dictionary<string, string> { ["persist:Count"] = "v1" };
        var bag = new PersistBag((IReadOnlyDictionary<string, string>)from);

        Assert.False(bag.TryGet("Count", new Throwing<int>(), out var value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void A_getter_that_throws_is_left_out_and_the_others_are_written()
    {
        var into = new Dictionary<string, string>();
        var bag = new PersistBag((IDictionary<string, string>)into);

        bag.Capture(new Vm(), "Bad", (Vm _) => ThrowInt());
        bag.Capture(new Vm(), "Count", vm => vm.Count);

        Assert.Equal(new[] { "persist:Count" }, into.Keys.ToArray());
    }

    [Fact]
    public void A_setter_that_throws_does_not_stop_the_restore()
    {
        var from = new Dictionary<string, string> { ["persist:Count"] = "v5" };
        var bag = new PersistBag((IReadOnlyDictionary<string, string>)from);
        var vm = new Vm();

        bag.Restore<Vm, int>(vm, "Count", (_, _) => throw new InvalidOperationException("no"));
        bag.Restore<Vm, int>(vm, "Count", (v, value) => v.Count = value);

        Assert.Equal(5, vm.Count);
    }

    private static int ThrowInt() => throw new InvalidOperationException("no");

    [Fact]
    public void A_character_a_date_a_time_and_an_offset_are_written_as_text_and_read_back()
    {
        var into = new Dictionary<string, string>();
        var write = new PersistBag((IDictionary<string, string>)into);
        var offset = new DateTimeOffset(2026, 11, 21, 17, 40, 0, TimeSpan.FromHours(1));
        var date = new DateOnly(2026, 11, 21);
        var time = new TimeOnly(17, 40, 5);

        Assert.True(write.Set("C", 'x'));
        Assert.True(write.Set("O", offset));
        Assert.True(write.Set("D", date));
        Assert.True(write.Set("T", time));

        Assert.Equal("vx", into["persist:C"]);
        var read = new PersistBag((IReadOnlyDictionary<string, string>)into);
        Assert.True(read.TryGet<char>("C", out var c));
        Assert.True(read.TryGet<DateTimeOffset>("O", out var o));
        Assert.True(read.TryGet<DateOnly>("D", out var d));
        Assert.True(read.TryGet<TimeOnly>("T", out var t));
        Assert.Equal('x', c);
        Assert.Equal(offset, o);
        Assert.Equal(date, d);
        Assert.Equal(time, t);
    }
}
