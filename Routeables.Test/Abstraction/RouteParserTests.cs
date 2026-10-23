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
    public void TryMatch_reports_an_unsupported_constraint()
    {
        var exception = Assert.Throws<NotSupportedException>(
            () => RouteParser.TryMatch("items/{id:unsupported}", "items/value"));

        Assert.Contains("unsupported", exception.Message);
    }
}
