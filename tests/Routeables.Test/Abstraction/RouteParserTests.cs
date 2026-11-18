using Routeables.Routing;

namespace Routeables.Test.Abstraction;

public class RouteParserTests
{
    [Fact]
    public void TryMatch_converts_all_builtin_constraints_using_invariant_formats()
    {
        var args = RouteableArgs.Create();
        var id = Guid.NewGuid();
        var route = $"items/12/2147483648/true/{id}/1234.56/250";

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
    public void A_brace_in_the_address_does_not_swallow_its_query()
    {
        var args = RouteableArgs.Create();

        Assert.True(RouteParser.TryMatch("s/{q}?{tab?}", "s/a}b?tab=2", args, "tab"));

        Assert.Equal("a}b", args.ViewModelParameters.Concat(args.ViewParameters).Single(x => x.name == "q").value);
        Assert.Equal("2", args.ViewParameters.Single(x => x.name == "tab").value);
    }

    [Fact]
    public void A_fixed_entry_of_the_query_of_a_template_must_be_in_the_address()
    {
        Assert.True(RouteParser.TryMatch("list?mode=grid", "list?mode=grid"));
        Assert.False(RouteParser.TryMatch("list?mode=grid", "list?mode=table"));
        Assert.False(RouteParser.TryMatch("list?mode=grid", "list"));
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
    public void A_type_with_no_namespace_keeps_its_name_when_a_homonym_of_a_namespace_is_registered()
    {
        var options = new System.Text.Json.JsonSerializerOptions { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };
        var json = "p/" + Uri.EscapeDataString("{\"Name\":\"a\"}");
        try
        {
            RouteParser.RegisterSerializer<global::Patron>(options.GetTypeInfo(typeof(global::Patron)));
            RouteParser.RegisterSerializer<global::HomonymSales.Patron>(options.GetTypeInfo(typeof(global::HomonymSales.Patron)));

            var plain = RouteableArgs.Create();
            Assert.True(RouteParser.TryMatch("p/{who:Patron}", json, plain));       // its only name: it was lost to the homonym
            Assert.IsType<global::Patron>(plain.ViewModelParameters.Single().value);

            var sales = RouteableArgs.Create();
            Assert.True(RouteParser.TryMatch("p/{who:HomonymSales.Patron}", json, sales));
            Assert.IsType<global::HomonymSales.Patron>(sales.ViewModelParameters.Single().value);
        }
        finally
        {
            RouteParser.UnregisterSerializer<global::Patron>();
            RouteParser.UnregisterSerializer<global::HomonymSales.Patron>();
        }
    }

    [Fact]
    public void A_registered_type_named_like_a_built_in_constraint_does_not_replace_it()
    {
        var options = new System.Text.Json.JsonSerializerOptions { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };
        var id = System.Guid.NewGuid();
        try
        {
            RouteParser.RegisterSerializer<global::HomonymShadow.Guid>(options.GetTypeInfo(typeof(global::HomonymShadow.Guid)));
            RouteParser.RegisterSerializer<global::HomonymShadow.String>(options.GetTypeInfo(typeof(global::HomonymShadow.String)));

            var guid = RouteableArgs.Create();
            Assert.True(RouteParser.TryMatch("g/{x:guid}", "g/" + id, guid));
            Assert.Equal(id, guid.ViewModelParameters.Single().value);

            var text = RouteableArgs.Create();
            Assert.True(RouteParser.TryMatch("s/{x:string}", "s/plain", text));
            Assert.Equal("plain", text.ViewModelParameters.Single().value);

            // The type itself is still reached by its full name.
            var typed = RouteableArgs.Create();
            Assert.True(RouteParser.TryMatch("g/{x:HomonymShadow.Guid}", "g/" + Uri.EscapeDataString("{\"Name\":\"a\"}"), typed));
            Assert.IsType<global::HomonymShadow.Guid>(typed.ViewModelParameters.Single().value);
        }
        finally
        {
            RouteParser.UnregisterSerializer<global::HomonymShadow.Guid>();
            RouteParser.UnregisterSerializer<global::HomonymShadow.String>();
        }
    }

    public sealed record Fragile(string Name);

    private sealed class FragileConverter : System.Text.Json.Serialization.JsonConverter<Fragile>
    {
        public override Fragile Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
            => throw new InvalidOperationException("the converter of the application fails");
        public override void Write(System.Text.Json.Utf8JsonWriter writer, Fragile value, System.Text.Json.JsonSerializerOptions options) { }
    }

    [Fact]
    public void A_converter_that_fails_with_another_exception_is_an_address_that_does_not_match()
    {
        var options = new System.Text.Json.JsonSerializerOptions { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };
        options.Converters.Add(new FragileConverter());
        try
        {
            RouteParser.RegisterSerializer<Fragile>(options.GetTypeInfo(typeof(Fragile)));

            Assert.False(RouteParser.TryMatch("f/{x:Fragile}", "f/" + Uri.EscapeDataString("{\"Name\":\"a\"}")));
        }
        finally
        {
            RouteParser.UnregisterSerializer<Fragile>();
        }
    }

    [Fact]
    public void Two_registered_types_named_like_a_built_in_constraint_do_not_disable_it()
    {
        var options = new System.Text.Json.JsonSerializerOptions { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };
        var id = System.Guid.NewGuid();
        try
        {
            RouteParser.RegisterSerializer<global::HomonymShadow.Guid>(options.GetTypeInfo(typeof(global::HomonymShadow.Guid)));
            RouteParser.RegisterSerializer<global::HomonymShadow2.Guid>(options.GetTypeInfo(typeof(global::HomonymShadow2.Guid)));     // "Guid" now stands for two types

            var guid = RouteableArgs.Create();
            Assert.True(RouteParser.TryMatch("g/{x:guid}", "g/" + id, guid));
            Assert.Equal(id, guid.ViewModelParameters.Single().value);
        }
        finally
        {
            RouteParser.UnregisterSerializer<global::HomonymShadow.Guid>();
            RouteParser.UnregisterSerializer<global::HomonymShadow2.Guid>();
        }
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("a/../b")]
    public void A_dot_segment_is_not_a_value_a_browser_would_resolve_it(string value)
    {
        Assert.Throws<ArgumentException>(() => RouteParser.Build("/users/{name}/edit", new { name = value }));
        Assert.Throws<ArgumentException>(() => RouteParser.Build("/files/{**path}", new { path = "x/" + value }));
        Assert.False(RouteParser.TryMatch("/users/{name}/edit", "/users/" + Uri.EscapeDataString(value) + "/edit"));
    }

    [Fact]
    public void A_dot_segment_is_refused_written_by_hand_too_and_a_query_value_is_not_a_segment()
    {
        Assert.False(RouteParser.TryMatch("/users/{name}/edit", "/users/../edit"));
        Assert.False(RouteParser.TryMatch("/users/{name}/edit", "/users/%2E%2E/edit"));
        Assert.False(RouteParser.TryMatch("/files/{**path}", "/files/a/../../admin"));
        Assert.True(RouteParser.TryMatch("/files/{**path}", "/files/a.b/c..d"));
        Assert.True(RouteParser.TryMatch("/list?{dir}", "/list?dir=.."));
        Assert.Equal("/list?dir=..", RouteParser.Build("/list?{dir}", new { dir = ".." }));
    }

    [Fact]
    public void Values_in_a_dictionary_of_any_type_are_read()
    {
        Assert.Equal("/p/43", RouteParser.Build("/p/{id:int}", new Dictionary<string, int> { ["id"] = 43 }));
        Assert.Equal("/p/a", RouteParser.Build("/p/{id}", new Dictionary<string, string> { ["id"] = "a" }));
    }

    [Fact]
    public void A_number_has_one_text_in_an_address()
    {
        // "42", "+42" and " 42" were three addresses for one page.
        foreach (var (template, accepted) in new[]
        {
            ("x/{n:int}", new[] { "42", "-42", "0" }), ("x/{n:long}", new[] { "42", "-4200000000000" }),
            ("x/{n:decimal}", new[] { "1.5", "-1.5", "10" }), ("x/{n:double}", new[] { "1.5", "-0.25", "1000", "1E+20" }),
        })
        {
            foreach (var text in accepted)
                Assert.True(RouteParser.TryMatch(template, "x/" + text), template + " " + text);
        }

        foreach (var (template, refused) in new[]
        {
            ("x/{n:int}", new[] { "+42", "%2042", "42%20", "4,2", "007", "-0" }), ("x/{n:long}", new[] { "+42", "%2042", "007" }),
            ("x/{n:decimal}", new[] { "+1.5", "%201.5", "1.5%20", "1,000.5", "007", "01.5" }), ("x/{n:double}", new[] { "+1.5", "%201.5", "NaN", "Infinity", "-Infinity", "7.0", "1e3", "07", "1.50" }),
        })
        {
            foreach (var text in refused)
                Assert.False(RouteParser.TryMatch(template, "x/" + text), template + " " + text);
        }
    }

    [Fact]
    public void A_guid_and_a_bool_have_one_text_in_an_address()
    {
        var id = Guid.NewGuid();
        Assert.True(RouteParser.TryMatch("x/{g:guid}", "x/" + id));
        Assert.False(RouteParser.TryMatch("x/{g:guid}", "x/{" + id + "}"));
        Assert.False(RouteParser.TryMatch("x/{g:guid}", "x/" + id.ToString("N")));
        Assert.True(RouteParser.TryMatch("x/{b:bool}", "x/True"));
        Assert.True(RouteParser.TryMatch("x/{b:bool}", "x/false"));
        Assert.False(RouteParser.TryMatch("x/{b:bool}", "x/%20true%20"));
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

    [Theory]
    [InlineData("a//b")]
    [InlineData("a/b/")]
    [InlineData("/a/b")]
    public void Build_refuses_a_catch_all_value_with_an_empty_segment(string value)
        => Assert.Throws<ArgumentException>(() => RouteParser.Build("/files/{**path}", new { path = value }));

    [Fact]
    public void Build_keeps_a_catch_all_value_made_of_plain_segments()
        => Assert.Equal("/files/a/b", RouteParser.Build("/files/{**path}", new { path = "a/b" }));

    [Fact]
    public void A_name_given_by_the_path_and_by_the_query_is_refused()
    {
        // "/dup/{id}?{id}": the value of the query was lost without a word.
        Assert.Throws<ArgumentException>(() => RouteParser.Build("/dup/{id}?{id}", new { id = 1 }));
        Assert.Throws<ArgumentException>(() => RouteParser.Build("/dup/{id}?{ID?}", new { id = 1 }));
        Assert.False(RouteParser.TryMatch("/dup/{id}?{id}", "/dup/1?id=2", RouteableArgs.Create()));
        Assert.Equal("/dup/1?x=2", RouteParser.Build("/dup/{id}?{x}", new { id = 1, x = 2 }));
    }

    [Fact]
    public void A_decimal_has_one_text_whatever_zeros_it_was_written_with()
    {
        Assert.True(RouteParser.TryMatch("x/{n:decimal}", "x/1.5", RouteableArgs.Create()));
        Assert.False(RouteParser.TryMatch("x/{n:decimal}", "x/1.50", RouteableArgs.Create()));
        Assert.False(RouteParser.TryMatch("x/{n:decimal}", "x/1.0", RouteableArgs.Create()));
        Assert.True(RouteParser.TryMatch("x/{n:decimal}", "x/10", RouteableArgs.Create()));
        Assert.Equal("x/1.5", RouteParser.Build("x/{n:decimal}", new { n = 1.50m }));
        Assert.Equal("x/10", RouteParser.Build("x/{n:decimal}", new { n = 10.00m }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Of_two_templates_with_the_same_path_the_one_that_uses_the_query_of_the_address_wins(bool reversed)
    {
        (string Template, Type? ViewModelType)[] entries = [("/search", typeof(string)), ("/search?{q?}", typeof(int))];
        if (reversed) Array.Reverse(entries);

        Assert.Equal("/search?{q?}", RouteParser.BestMatch(entries, "/search?q=1").Template);
        Assert.Equal("/search", RouteParser.BestMatch(new (string, Type?)[] { ("/search", typeof(string)), ("/search?{q?}", typeof(int)) }.Take(1), "/search").Template);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_fixed_query_entry_makes_a_template_more_exact_than_the_same_path_without_it(bool reversed)
    {
        (string Template, Type? ViewModelType)[] entries = [("/search", typeof(string)), ("/search?mode=grid", typeof(int))];
        if (reversed) Array.Reverse(entries);

        Assert.Equal("/search?mode=grid", RouteParser.BestMatch(entries, "/search?mode=grid").Template);
        Assert.Equal("/search", RouteParser.BestMatch(entries, "/search").Template);
        Assert.Equal("/search", RouteParser.BestMatch(entries, "/search?mode=list").Template);
    }

    [Fact]
    public void A_double_zero_has_one_text()
    {
        Assert.True(RouteParser.TryMatch("x/{n:double}", "x/0", RouteableArgs.Create()));
        Assert.False(RouteParser.TryMatch("x/{n:double}", "x/-0", RouteableArgs.Create()));
        Assert.Equal("x/0", RouteParser.Build("x/{n:double}", new { n = -0.0 }));
    }

    [Fact]
    public void A_template_literal_written_escaped_matches_its_own_output()
    {
        var args = RouteableArgs.Create();

        Assert.True(RouteParser.TryMatch("/my%20page/{id:int}", "/my%20page/3", args));
        Assert.True(RouteParser.TryMatch("/my%20page/{id:int}", "/my page/3", RouteableArgs.Create()));
        Assert.Equal("/my%20page/3", RouteParser.Build("/my%20page/{id:int}", new { id = 3 }));
    }
}
