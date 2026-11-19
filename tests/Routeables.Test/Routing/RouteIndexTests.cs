using Routeables.Routing;
using System.Collections.ObjectModel;

namespace Routeables.Test.Routing;

public class RouteIndexTests
{
    // Enough templates to be indexed (the index starts at 8), with the shapes that cannot be indexed under a literal.
    private static readonly string[] Templates =
    [
        "/p00", "/p01", "/p02", "/p03", "/p04", "/p05", "/p06", "/p07",
        "/detail/{id:int}", "/detail/new", "/detail/{id}", "/{slug}", "/{slug}/edit", "/files/{**path}",
        "/products/{tab?}", "/products", "/Orders/{id:int}?mode=grid", "/{a}/{b}/{c}", "/", "/{**all}",
    ];

    private static readonly string[] Addresses =
    [
        "/", "", "/p00", "/P07", "/p07/x", "/p99", "/detail/7", "/detail/new", "/detail/abc", "/detail", "/files/a/b/c", "/files",
        "/products", "/products/x", "/orders/5?mode=grid", "/orders/5?mode=list", "/x/y/z", "/zzz/edit", "//p00", "/p00#top",
        "/p00?x=1", "/%70%30%30", "?x=1", "/files/a%2Fb",
    ];

    private static (string Template, Type? ViewModelType)[] Entries() => [.. Templates.Select(t => (t, (Type?)null))];

    [Fact]
    public void The_index_finds_the_route_the_linear_search_finds_for_every_address()
    {
        var linear = Entries();
        var indexed = new ReadOnlyCollection<(string Template, Type? ViewModelType)>(Entries());

        foreach (var address in Addresses)
            Assert.True(RouteParser.BestMatch(linear, address).Template == RouteParser.BestMatch(indexed, address).Template, address);
    }

    [Fact]
    public void Equally_specific_templates_keep_the_order_of_registration_through_the_index()
    {
        // /{x}/a and /{y}/a are as specific as each other; /p00/{z} and /{w}/a differ in the first segment only through the index.
        string[] templates = ["/p00", "/p01", "/p02", "/p03", "/p04", "/p05", "/p06", "/p07", "/{x}/a", "/{y}/a", "/p00/{z}"];
        var table = new ReadOnlyCollection<(string Template, Type? ViewModelType)>([.. templates.Select(t => (t, (Type?)null))]);

        Assert.Equal("/{x}/a", RouteParser.BestMatch(table, "/q/a").Template);
        Assert.Equal("/p00/{z}", RouteParser.BestMatch(table, "/p00/a").Template);   // a literal first beats a parameter first
    }

    [Fact]
    public void A_string_constraint_is_untyped_whatever_its_case()
    {
        // {id:String} reads the text as {id} does: it must not rank as a typed placeholder and tie with {id:int}.
        foreach (var untyped in new[] { "/a/{id:String}", "/a/{id:STRING}", "/a/{id:string}", "/a/{id}" })
        {
            var table = new ReadOnlyCollection<(string Template, Type? ViewModelType)>([(untyped, null), ("/a/{id:int}", null)]);
            Assert.Equal("/a/{id:int}", RouteParser.BestMatch(table, "/a/42").Template);
            Assert.Equal(untyped, RouteParser.BestMatch(table, "/a/abc").Template);
        }
    }

    [Fact]
    public void A_typed_optional_placeholder_beats_an_untyped_optional_one()
    {
        // The same two templates in both orders: the one that reads a number comes first for a number, the other for the rest.
        foreach (var templates in new[] { new[] { "/a/{id:int?}", "/a/{tab?}" }, new[] { "/a/{tab?}", "/a/{id:int?}" } })
        {
            var table = new ReadOnlyCollection<(string Template, Type? ViewModelType)>([.. templates.Select(t => (t, (Type?)null))]);
            Assert.Equal("/a/{id:int?}", RouteParser.BestMatch(table, "/a/5").Template);
            Assert.Equal("/a/{tab?}", RouteParser.BestMatch(table, "/a/x").Template);
        }
    }

    [Fact]
    public void An_address_that_starts_with_no_registered_literal_still_reaches_the_parameter_templates()
    {
        var table = new ReadOnlyCollection<(string Template, Type? ViewModelType)>(Entries());

        Assert.Equal("/{slug}", RouteParser.BestMatch(table, "/unknown").Template);
        Assert.Equal("/", RouteParser.BestMatch(table, "/").Template);
    }

    [Fact]
    public void A_table_that_gets_a_route_is_a_new_collection_with_its_own_index()
    {
        var before = new ReadOnlyCollection<(string Template, Type? ViewModelType)>(Entries());
        Assert.Equal("/{slug}", RouteParser.BestMatch(before, "/late").Template);

        var after = new ReadOnlyCollection<(string Template, Type? ViewModelType)>([.. Entries(), ("/late", null)]);

        Assert.Equal("/late", RouteParser.BestMatch(after, "/late").Template);
        Assert.Equal("/{slug}", RouteParser.BestMatch(before, "/late").Template);
    }

    [Theory]
    [InlineData("users/{id:int}", "users/12", true)]
    [InlineData("users/{id:int}", "users/x", false)]
    [InlineData("users/{id:int}", "users", false)]
    [InlineData("users/{id:int}", "users/1/2", false)]
    [InlineData("users/{id:int?}", "users", true)]
    [InlineData("users/{tab?}/end", "users/end", false)]                         // an optional that is not last never matches
    [InlineData("files/{**rest}/x", "files/a/x", false)]                         // nor a catch-all that is not last
    [InlineData("a/{id:nothing}", "a/1", false)]                                 // an unknown constraint
    [InlineData("search?{q}", "search?q=a", true)]
    [InlineData("search?{q}", "search", false)]
    [InlineData("grid?mode=grid", "grid?mode=list", false)]
    public void An_answer_without_arguments_is_the_one_that_has_a_reason_when_it_is_no(string template, string address, bool expected)
    {
        Assert.Equal(expected, RouteParser.TryMatch(template, address));
        Assert.Equal(expected, RouteParser.Mismatch(template, address) is null);
        if (!expected)
            Assert.False(string.IsNullOrEmpty(RouteParser.Mismatch(template, address)));   // the reason is still given to who asks for it
    }

    [Fact]
    public void An_answer_without_arguments_binds_nothing_and_with_arguments_still_binds()
    {
        var args = RouteableArgs.Create();

        Assert.True(RouteParser.TryMatch("users/{id:int}", "users/12?from=a", args));

        Assert.Equal(12, args.ViewModelParameters.Single(x => x.name == "id").value);
        Assert.Contains(args.Values, x => x.name == "from");
    }
}
