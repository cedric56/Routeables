using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Tests.Pages;
using System.Dynamic;
using SecondViewFor = Routeables.Tests.Pages.SecondViewFor;

namespace Routeables.Tests.Core;

/// <summary>The edges of the addresses the router reads and writes: qualifiers on the first page, the template of a route, and the values given to a template.</summary>
public class RouterAddressEdgeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_go_back_then_to_route_on_the_first_page_has_nothing_to_go_back_to()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<FakeViewFor, FakeViewModel>(route: "/home");
        services.RegisterRoute<SecondViewFor, SecondViewModel>(route: "/orders");
        await using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/home", cancellationToken: Ct);
        var shown = router.CurrentViewModel;

        var result = await router.GoToAsync("-/orders", cancellationToken: Ct);

        Assert.False(result.Success);
        Assert.Same(shown, router.CurrentViewModel);
    }

    [Fact]
    public async Task The_template_of_a_route_is_found_by_the_template_itself_by_a_match_or_not_at_all()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<FakeViewFor, FakeViewModel>(route: "/orders/{id:int}");
        await using var provider = services.BuildServiceProvider();
        var templates = (IRouteTemplates)provider.GetRequiredService<IRouter>();

        Assert.Equal("/orders/{id:int}", templates.TemplateOf("/orders/{id:int}"));
        Assert.Equal("/orders/{id:int}", templates.TemplateOf("/orders/42"));
        Assert.Null(templates.TemplateOf("/nowhere"));
    }

    [Fact]
    public async Task The_template_of_a_route_is_found_by_its_path_when_no_template_matches_it()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        // "mode=grid" is a fixed part of the template: an address with another mode is not matched by it, but it is the same path.
        services.RegisterRoute<FakeViewFor, FakeViewModel>(route: "/search?mode=grid");
        await using var provider = services.BuildServiceProvider();
        var templates = (IRouteTemplates)provider.GetRequiredService<IRouter>();

        Assert.Equal("/search?mode=grid", templates.TemplateOf("/search?mode=grid"));
        Assert.Equal("/search?mode=grid", templates.TemplateOf("/search?mode=list"));
        Assert.Equal("/search?mode=grid", templates.TemplateOf("/SEARCH?mode=list"));
        Assert.Null(templates.TemplateOf("/other?mode=list"));
    }

    [Fact]
    public async Task The_template_of_a_route_is_found_in_the_menu_when_the_table_has_none()
    {
        var menu = new FakeMenuViewModel();
        menu.Entries.Add(new RouteableMenuEntry("/reports/monthly", "/reports/{period}", "Reports"));
        menu.Entries.Add(new RouteableMenuEntry("/untemplated", string.Empty, "None"));
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.AddSingleton<IRouteableMenuViewModel>(menu);
        await using var provider = services.BuildServiceProvider();
        var templates = (IRouteTemplates)provider.GetRequiredService<IRouter>();

        Assert.Equal("/reports/{period}", templates.TemplateOf("/reports/monthly"));
        Assert.Equal("/reports/{period}", templates.TemplateOf("/Reports/monthly?x=1"));
        Assert.Null(templates.TemplateOf("/untemplated"));   // an entry without a template names nothing
        Assert.Null(templates.TemplateOf("/reports/yearly"));
    }

    private sealed class ReadOnlyOnly(Dictionary<string, object?> inner) : IReadOnlyDictionary<string, object?>
    {
        public object? this[string key] => inner[key];
        public IEnumerable<string> Keys => inner.Keys;
        public IEnumerable<object?> Values => inner.Values;
        public int Count => inner.Count;
        public bool ContainsKey(string key) => inner.ContainsKey(key);
        public bool TryGetValue(string key, out object? value) => inner.TryGetValue(key, out value);
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => inner.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void No_values_gives_the_address_of_a_template_whose_values_are_all_optional()
    {
        Assert.Equal("orders", RouteParser.Build("orders/{id?}", null));
    }

    [Fact]
    public void Values_are_read_from_a_read_only_dictionary()
    {
        Assert.Equal("orders/7", RouteParser.Build("orders/{id}", new ReadOnlyOnly(new() { ["id"] = 7 })));
    }

    [Fact]
    public void Values_are_read_from_a_generic_dictionary_that_is_not_read_only()
    {
        IDictionary<string, object?> values = new ExpandoObject();
        values["id"] = 8;

        Assert.Equal("orders/8", RouteParser.Build("orders/{id}", values));
    }

    [Fact]
    public void Values_are_read_from_a_dictionary_of_another_value_type()
    {
        Assert.Equal("orders/9", RouteParser.Build("orders/{id}", new Dictionary<string, int> { ["id"] = 9 }));
    }

    [Fact]
    public void Values_are_read_from_the_properties_of_an_object()
    {
        Assert.Equal("orders/10", RouteParser.Build("orders/{id}", new { id = 10 }));
    }

    [Fact]
    public void The_query_of_an_address_leaves_the_fragment_out()
    {
        var query = RouteParser.QueryOf("/search?mode=grid&page=2#results");

        Assert.Equal("grid", query["mode"]);
        Assert.Equal("2", query["page"]);
        Assert.Equal(2, query.Count);
    }
}
