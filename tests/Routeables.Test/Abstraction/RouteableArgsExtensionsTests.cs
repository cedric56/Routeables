using Routeables.Abstraction.Routing;
using Routeables.Routing;

namespace Routeables.Tests.Abstraction;

/// <summary>Reading the neutral values of the arguments by name, by type and by typed key, and writing them under a typed key.</summary>
public class RouteableArgsExtensionsTests
{
    private static readonly RouteableKey<string> Name = new("name");
    private static readonly RouteableKey<int> Count = new("count");

    // ------------------------------------------------------------------------------------------------ by name

    [Fact]
    public void A_value_is_read_by_its_name_with_its_type()
    {
        var args = RouteableArgs.Create().WithValue("count", 3).WithValue("name", "Ada");

        Assert.Equal(3, args.Get<int>("count"));
        Assert.Equal("Ada", args.Get<string>("name"));
    }

    [Fact]
    public void A_missing_value_or_a_null_one_gives_the_default()
    {
        var args = RouteableArgs.Create().WithValue("nothing", null);

        Assert.Null(args.Get<string>("absent"));
        Assert.Equal(0, args.Get<int>("absent"));
        Assert.Null(args.Get<string>("nothing"));
    }

    [Fact]
    public void A_value_of_another_type_is_refused_with_both_types_named()
    {
        var args = RouteableArgs.Create().WithValue("count", 3);

        var ex = Assert.Throws<InvalidCastException>(() => args.Get<string>("count"));

        Assert.Contains("Int32", ex.Message);
        Assert.Contains("String", ex.Message);
    }

    [Fact]
    public void Two_values_with_one_name_are_refused()
    {
        var args = RouteableArgs.Create().WithValue("dup", 1).WithValue("dup", 2);

        var ex = Assert.Throws<InvalidOperationException>(() => args.Get<int>("dup"));

        Assert.Contains("dup", ex.Message);
        Assert.Throws<InvalidOperationException>(() => args.TryGet<int>("dup", out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_name_is_refused(string? name)
    {
        var args = RouteableArgs.Create();

        Assert.ThrowsAny<ArgumentException>(() => args.Get<int>(name!));
        Assert.ThrowsAny<ArgumentException>(() => args.TryGet<int>(name!, out _));
    }

    [Fact]
    public void No_arguments_are_refused()
    {
        IRouteableArgs args = null!;

        Assert.Throws<ArgumentNullException>(() => args.Get<int>("a"));
        Assert.Throws<ArgumentNullException>(() => args.TryGet<int>("a", out _));
        Assert.Throws<ArgumentNullException>(() => args.Get<int>());
        Assert.Throws<ArgumentNullException>(() => args.WithValue(Count, 1));
        Assert.Throws<ArgumentNullException>(() => args.WithViewModel(Count, 1));
        Assert.Throws<ArgumentNullException>(() => args.WithView(Count, 1));
    }

    [Fact]
    public void TryGet_tells_whether_the_value_is_there_with_that_type()
    {
        var args = RouteableArgs.Create().WithValue("count", 3).WithValue("none", null);

        Assert.True(args.TryGet<int>("count", out var count));
        Assert.Equal(3, count);
        Assert.False(args.TryGet<string>("count", out var wrong));
        Assert.Null(wrong);
        Assert.False(args.TryGet<int>("absent", out var absent));
        Assert.Equal(0, absent);
        Assert.False(args.TryGet<string>("none", out _));
    }

    // ------------------------------------------------------------------------------------------------ by type

    [Fact]
    public void A_value_is_read_by_its_type_when_it_is_the_only_one()
    {
        var args = RouteableArgs.Create().WithValue("a", "text").WithValue("b", 7);

        Assert.Equal("text", args.Get<string>());
        Assert.Equal(7, args.Get<int>());
        Assert.Null(args.Get<Uri>());
    }

    [Fact]
    public void Two_values_of_one_type_are_refused_and_the_message_says_to_name_them()
    {
        var args = RouteableArgs.Create().WithValue("a", "one").WithValue("b", "two");

        var ex = Assert.Throws<InvalidOperationException>(() => args.Get<string>());

        Assert.Contains("2", ex.Message);
        Assert.Contains("String", ex.Message);
        Assert.Equal("one", args.Get<string>("a"));
    }

    [Fact]
    public void A_value_is_read_by_a_type_it_derives_from()
    {
        var args = RouteableArgs.Create().WithValue("list", new List<int> { 1 });

        Assert.Single(args.Get<IEnumerable<int>>()!);
    }

    // ------------------------------------------------------------------------------------------------ typed keys

    [Fact]
    public void A_typed_key_writes_and_reads_a_value_with_its_type()
    {
        var args = RouteableArgs.Create().WithValue(Name, "Ada").WithValue(Count, 2);

        Assert.Equal("Ada", args.Get(Name));
        Assert.Equal(2, args.Get(Count));
        Assert.True(args.TryGet(Name, out var name));
        Assert.Equal("Ada", name);
        Assert.False(args.TryGet(new RouteableKey<int>("absent"), out _));
    }

    [Fact]
    public void A_key_names_the_value_it_was_written_under()
    {
        var args = RouteableArgs.Create().WithValue(Count, 5);

        Assert.Equal(5, args.Get<int>("count"));
        Assert.Equal("count", Count.Name);
        Assert.Equal(new RouteableKey<int>("count"), Count);
    }

    [Fact]
    public void A_key_reads_what_the_name_alone_would_and_refuses_what_it_refuses()
    {
        var args = RouteableArgs.Create().WithValue("count", "not a number");

        Assert.Throws<InvalidCastException>(() => args.Get(Count));
    }

    [Fact]
    public void The_view_model_and_the_view_values_under_a_key_go_to_their_own_list()
    {
        var args = RouteableArgs.Create().WithViewModel(Name, "vm", attribute: true).WithView(Count, 9);

        var vm = Assert.Single(args.ViewModelParameters);
        Assert.Equal("name", vm.name);
        Assert.Equal("vm", vm.value);
        Assert.True(vm.attribute);
        var view = Assert.Single(args.ViewParameters);
        Assert.Equal("count", view.name);
        Assert.Equal(9, view.value);
        Assert.False(view.attribute);
        Assert.Empty(args.Values);
    }
}
