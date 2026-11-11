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
    public void A_plus_in_a_query_is_a_space_and_in_a_path_it_is_a_plus()
    {
        var args = RouteableArgs.Create();

        Assert.True(RouteParser.TryMatch("files/{name}?{tag}", "files/a+b?tag=x+y%2Bz", args));

        Assert.Equal("a+b", args.ViewModelParameters.Single(x => x.name == "name").value);   // a path segment is not a query
        Assert.Equal("x y+z", args.ViewModelParameters.Single(x => x.name == "tag").value);  // + is a space, %2B is a plus
    }

    [Theory]
    [InlineData("a b")]
    [InlineData("a+b")]
    [InlineData("a b+c&d=e")]
    public void A_query_value_written_by_Build_is_read_back_as_it_was(string value)
    {
        var address = RouteParser.Build("files?{tag}", new Dictionary<string, object?> { ["tag"] = value });

        var back = RouteableArgs.Create();
        Assert.True(RouteParser.TryMatch("files?{tag}", address, back));
        Assert.Equal(value, back.ViewModelParameters.Single(x => x.name == "tag").value);
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
    public void A_shorter_template_beats_the_optional_one_that_extends_it_in_any_order_of_registration()
    {
        (string Template, Type? ViewModelType)[] optionalFirst = [("products/{tab?}", null), ("products", null)];
        (string Template, Type? ViewModelType)[] exactFirst = [("products", null), ("products/{tab?}", null)];

        Assert.Equal("products", RouteParser.BestMatch(optionalFirst, "products").Template);
        Assert.Equal("products", RouteParser.BestMatch(exactFirst, "products").Template);
        Assert.Equal("products/{tab?}", RouteParser.BestMatch(optionalFirst, "products/specs").Template);
    }

    [Fact]
    public void A_shorter_template_beats_the_catch_all_that_extends_it()
    {
        (string Template, Type? ViewModelType)[] entries = [("files/{**path}", null), ("files", null)];

        Assert.Equal("files", RouteParser.BestMatch(entries, "files").Template);
        Assert.Equal("files/{**path}", RouteParser.BestMatch(entries, "files/a/b").Template);
    }

    [Fact]
    public void Equally_specific_templates_keep_the_order_of_registration()
    {
        (string Template, Type? ViewModelType)[] entries = [("a/{x}", null), ("a/{y}", null)];

        Assert.Equal("a/{x}", RouteParser.BestMatch(entries, "a/1").Template);
    }

    [Fact]
    public void No_matching_template_gives_an_empty_entry()
        => Assert.Null(RouteParser.BestMatch(Bears, "cats/1").Template);

    [Theory]
    [InlineData("customers/{id:int}", "not-an-integer")]
    [InlineData("customers/{id:int}", 3000000000L)]
    [InlineData("customers/{id:guid}", "42")]
    [InlineData("customers/{id:bool}", "maybe")]
    [InlineData("customers?{id:int}", "x")]
    public void Build_refuses_a_value_that_its_own_constraint_would_refuse(string template, object value)
    {
        var error = Assert.Throws<ArgumentException>(() => RouteParser.Build(template, new Dictionary<string, object?> { ["id"] = value }));
        Assert.Contains("id", error.Message);
    }

    [Theory]
    [InlineData("customers/{id:int}", 42)]
    [InlineData("customers/{id:int}", "42")]
    [InlineData("customers/{id:long}", 3000000000L)]
    [InlineData("customers/{id:guid}", "8f1b8d5e-0d64-4b0e-9d6c-0a0b6a6c3a11")]
    [InlineData("customers/{id:decimal}", 12.5)]
    [InlineData("customers/{id:double}", 0.1)]
    [InlineData("customers/{id}", "any text/with slash")]
    public void What_Build_writes_matches_the_template(string template, object value)
    {
        var address = RouteParser.Build(template, new Dictionary<string, object?> { ["id"] = value });
        Assert.True(RouteParser.TryMatch(template, address));
    }

    [Fact]
    public void An_optional_placeholder_that_is_not_last_never_matches_says_why_and_cannot_be_built()
    {
        Assert.Contains("last segment", RouteParser.Mismatch("tabs/{tab?}/end", "tabs/x/end"));
        Assert.Contains("last segment", RouteParser.Mismatch("tabs/{tab?}/end", "tabs/end"));
        Assert.Throws<ArgumentException>(() => RouteParser.Build("tabs/{tab?}/end", new Dictionary<string, object?>()));
        Assert.Null(RouteParser.Mismatch("tabs/{tab?}", "tabs"));
    }

    public sealed record ScratchPayload(string Name);

    [Fact]
    public void A_registered_type_can_be_taken_out_again_without_touching_the_others()
    {
        var options = new System.Text.Json.JsonSerializerOptions { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };
        RouteParser.RegisterSerializer<ScratchPayload>(options.GetTypeInfo(typeof(ScratchPayload)));
        var address = "p/" + Uri.EscapeDataString("{\"Name\":\"a\"}");
        Assert.True(RouteParser.TryMatch("p/{x:ScratchPayload}", address));

        RouteParser.UnregisterSerializer<ScratchPayload>();

        Assert.False(RouteParser.TryMatch("p/{x:ScratchPayload}", address));
        Assert.True(RouteParser.TryMatch("p/{x:int}", "p/1"));       // the built-in constraints are untouched
    }

    [Fact]
    public void RegisterSerializer_refuses_information_for_another_type()
    {
        var other = new System.Text.Json.JsonSerializerOptions { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() }
            .GetTypeInfo(typeof(global::HomonymSales.Customer));
        Assert.Throws<ArgumentException>(() => RouteParser.RegisterSerializer<global::HomonymSupport.Customer>(other));
    }

    [Fact]
    public void Two_types_with_one_simple_name_are_told_apart_by_their_full_name()
    {
        var options = new System.Text.Json.JsonSerializerOptions { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };
        RouteParser.RegisterSerializer<global::HomonymSales.Customer>(options.GetTypeInfo(typeof(global::HomonymSales.Customer)));
        RouteParser.RegisterSerializer<global::HomonymSupport.Customer>(options.GetTypeInfo(typeof(global::HomonymSupport.Customer)));

        Assert.Contains("several registered types", RouteParser.Mismatch("c/{who:Customer}", "c/%7B%7D"));

        var sales = RouteableArgs.Create();
        Assert.True(RouteParser.TryMatch("c/{who:" + typeof(global::HomonymSales.Customer).FullName + "}", "c/" + Uri.EscapeDataString("{\"Name\":\"a\"}"), sales));
        Assert.IsType<global::HomonymSales.Customer>(sales.ViewModelParameters.Single().value);

        var support = RouteableArgs.Create();
        Assert.True(RouteParser.TryMatch("c/{who:" + typeof(global::HomonymSupport.Customer).FullName + "}", "c/" + Uri.EscapeDataString("{\"Name\":\"a\"}"), support));
        Assert.IsType<global::HomonymSupport.Customer>(support.ViewModelParameters.Single().value);
    }

    [Fact]
    public void Optional_placeholders_one_after_the_other_at_the_end_match_and_build()
    {
        Assert.Null(RouteParser.Mismatch("cat/{age:int?}/{nick?}", "cat/7/Tom"));
        Assert.Null(RouteParser.Mismatch("cat/{age:int?}/{nick?}", "cat/7"));
        Assert.Null(RouteParser.Mismatch("cat/{age:int?}/{nick?}", "cat"));
        Assert.Equal("cat/7/Tom", RouteParser.Build("cat/{age:int?}/{nick?}", new Dictionary<string, object?> { ["age"] = 7, ["nick"] = "Tom" }));
        Assert.Equal("cat/7", RouteParser.Build("cat/{age:int?}/{nick?}", new Dictionary<string, object?> { ["age"] = 7 }));
        Assert.Equal("cat", RouteParser.Build("cat/{age:int?}/{nick?}", new Dictionary<string, object?>()));
    }

    [Fact]
    public void Build_refuses_a_later_value_when_an_earlier_optional_is_left_out()
        => Assert.Throws<ArgumentException>(() => RouteParser.Build("cat/{age:int?}/{nick?}", new Dictionary<string, object?> { ["nick"] = "Tom" }));
}
