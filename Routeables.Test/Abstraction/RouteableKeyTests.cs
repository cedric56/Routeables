using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Test.Abstraction;

public class RouteableKeyTests
{
    private static readonly RouteableKey<string> Title = new("title");
    private static readonly RouteableKey<int> Count = new("count");

    [Fact]
    public void A_value_written_under_a_key_is_read_back_with_the_same_key()
    {
        var args = RouteableArgs.Create().WithValue(Title, "hello").WithValue(Count, 3);

        Assert.Equal("hello", args.Get(Title));
        Assert.Equal(3, args.Get(Count));
    }

    [Fact]
    public void A_key_is_the_same_as_its_string_name()
    {
        var args = RouteableArgs.Create().WithValue(Title, "hello");

        Assert.Equal("hello", args.Get<string>("title"));
    }

    [Fact]
    public void TryGet_with_a_key_reports_a_missing_value()
    {
        var args = RouteableArgs.Create().WithValue(Title, "hello");

        Assert.True(args.TryGet(Title, out var title));
        Assert.Equal("hello", title);
        Assert.True(!args.TryGet(Count, out _));
    }
}
