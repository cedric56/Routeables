using Routeables.Abstraction;
using Routeables.Routing;

namespace Routeables.Test.Abstraction;

public class RouteParserTests
{
    [Fact]
    public void TryMatch_converts_all_builtin_constraints_using_invariant_formats()
    {
        var args = RouteableArgs.Create();
        var id = Guid.NewGuid();
        var route = $"items/12/2147483648/true/{id}/1234.56/2.5e2";

        var matched = RouteParser.TryMatch(
            "items/{intValue:int}/{longValue:long}/{enabled:bool}/{id:guid}/{price:decimal}/{ratio:double}",
            route,
            args);

        Assert.True(matched);
        Assert.Equal(12, args.ViewModelParameters.Single(x => x.name == "intValue").value);
        Assert.Equal(2147483648L, args.ViewModelParameters.Single(x => x.name == "longValue").value);
        Assert.Equal(true, args.ViewModelParameters.Single(x => x.name == "enabled").value);
        Assert.Equal(id, args.ViewModelParameters.Single(x => x.name == "id").value);
        Assert.Equal(1234.56m, args.ViewModelParameters.Single(x => x.name == "price").value);
        Assert.Equal(250d, args.ViewModelParameters.Single(x => x.name == "ratio").value);
    }

    [Fact]
    public void TryMatch_routes_query_parameters_to_the_view_or_view_model_and_keeps_unknown_values_as_neutral_values()
    {
        var args = RouteableArgs.Create();

        var matched = RouteParser.TryMatch(
            "orders/{id:int}?{tab?}&{enabled:bool}",
            "orders/42?tab=items&enabled=true&source=mail",
            args,
            "tab");

        Assert.True(matched);
        var tab = Assert.Single(args.ViewParameters);
        Assert.Equal("tab", tab.name);
        Assert.Equal("items", tab.value);
        Assert.False(tab.attribute);

        Assert.Equal(true, args.ViewModelParameters.Single(x => x.name == "enabled").value);

        // Not declared: a neutral value for the lifecycle hooks, never a constructor argument or an injected property.
        Assert.DoesNotContain(args.ViewModelParameters, x => x.name == "source");
        Assert.DoesNotContain(args.ViewParameters, x => x.name == "source");
        var source = Assert.Single(args.Values, x => x.name == "source");
        Assert.Equal("mail", source.value);
    }

    [Fact]
    public void TryMatch_accepts_a_missing_optional_segment_and_rejects_missing_or_invalid_required_segments()
    {
        Assert.True(RouteParser.TryMatch("orders/{id:int}/{tab?}", "orders/42"));
        Assert.False(RouteParser.TryMatch("orders/{id:int}", "orders"));
        Assert.False(RouteParser.TryMatch("orders/{id:int}", "orders/not-an-int"));
        Assert.False(RouteParser.TryMatch("orders/{id:int}", "orders/42/extra"));
        Assert.False(RouteParser.TryMatch("orders/{id:int}", "products/42"));
    }

    [Fact]
    public void TryMatch_decodes_path_and_query_values_and_uses_the_last_duplicate_query_value()
    {
        var args = RouteableArgs.Create();

        var matched = RouteParser.TryMatch(
            "files/{name}?{tag}",
            "FILES/a%20b?TAG=first&TAG=last%20value",
            args);

        Assert.True(matched);
        Assert.Equal("a b", args.ViewModelParameters.Single(x => x.name == "name").value);
        Assert.Equal("last value", args.ViewModelParameters.Single(x => x.name == "tag").value);
    }

    [Fact]
    public void An_unknown_constraint_never_matches_and_says_why_instead_of_throwing()
    {
        Assert.False(RouteParser.TryMatch("items/{id:unsupported}", "items/value"));

        var reason = RouteParser.Mismatch("items/{id:unsupported}", "items/value");

        Assert.NotNull(reason);
        Assert.Contains("unsupported", reason);
        Assert.Contains("RegisterSerializer", reason);
    }

    [Fact]
    public void An_unknown_constraint_is_reported_even_when_the_optional_value_is_missing()
        => Assert.Contains("unknown", RouteParser.Mismatch("items/{id:unsupported?}", "items"));

    [Fact]
    public void An_encoded_slash_stays_inside_its_segment()
    {
        var args = RouteableArgs.Create();

        Assert.True(RouteParser.TryMatch("files/{name}", "files/a%2Fb", args));
        Assert.Equal("a/b", args.ViewModelParameters.Single(x => x.name == "name").value);
        Assert.False(RouteParser.TryMatch("files/{name}", "files/a/b"));   // a real slash is another segment
    }

    [Fact]
    public void Unicode_and_invalid_escape_sequences_do_not_throw()
    {
        var args = RouteableArgs.Create();

        Assert.True(RouteParser.TryMatch("cities/{name}", "cities/%C3%89vry", args));
        Assert.Equal("Évry", args.ViewModelParameters.Single(x => x.name == "name").value);

        args = RouteableArgs.Create();
        Assert.True(RouteParser.TryMatch("cities/{name}", "cities/100%zz", args));   // not an escape: kept as it is
        Assert.Equal("100%zz", args.ViewModelParameters.Single(x => x.name == "name").value);
    }

    [Theory]
    [InlineData("tabs/{tab?}", "tabs", true)]
    [InlineData("tabs/{tab?}", "tabs/info", true)]
    [InlineData("tabs/{tab?}", "tabs/info/more", false)]
    [InlineData("tabs/{tab?}/end", "tabs/end", false)]   // an optional placeholder is for the last segment
    [InlineData("items/{id:int}", "items/abc", false)]
    [InlineData("items/{id:int}", "items", false)]
    public void Optional_and_required_placeholders(string template, string route, bool matches)
        => Assert.Equal(matches, RouteParser.TryMatch(template, route));

    [Fact]
    public void An_optional_query_parameter_may_be_missing_but_a_present_one_must_convert()
    {
        Assert.True(RouteParser.TryMatch("bears?{page:int?}", "bears"));
        Assert.True(RouteParser.TryMatch("bears?{page:int?}", "bears?page=2"));
        Assert.False(RouteParser.TryMatch("bears?{page:int?}", "bears?page=two"));
    }

    private static readonly (string Template, Type? ViewModelType)[] Bears =
    [
        ("bears/{id}", null),
        ("bears/{id:int}", null),
        ("bears/new", null),
        ("bears/{tab?}", null),
    ];

    [Theory]
    [InlineData("bears/new", "bears/new")]             // a literal beats the parameters, whatever the order of registration
    [InlineData("bears/12", "bears/{id:int}")]         // a typed parameter beats an untyped one
    [InlineData("bears/polar", "bears/{id}")]          // an untyped one beats an optional one
    [InlineData("bears", "bears/{tab?}")]
    public void The_most_specific_template_wins(string route, string expected)
        => Assert.Equal(expected, RouteParser.BestMatch(Bears, route).Template);

    [Fact]
    public void Equally_specific_templates_keep_the_order_of_registration()
    {
        (string Template, Type? ViewModelType)[] entries = [("a/{x}", null), ("a/{y}", null)];

        Assert.Equal("a/{x}", RouteParser.BestMatch(entries, "a/1").Template);
    }

    [Fact]
    public void No_matching_template_gives_an_empty_entry()
        => Assert.Null(RouteParser.BestMatch(Bears, "cats/1").Template);
}
