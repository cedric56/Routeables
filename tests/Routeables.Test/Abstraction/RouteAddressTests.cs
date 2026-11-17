using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;

namespace Routeables.Tests.Abstraction;

/// <summary>"The same route?" is answered in one place, whoever asks: the router, the menus, the guards, the controls and the address bar.</summary>
public class RouteAddressTests
{
    [Fact]
    public void The_slashes_around_a_path_and_a_query_do_not_make_two_paths()
    {
        foreach (var other in new[] { "about", "/about", "/about/", "about/", "/about?x=1", "//about" })
            Assert.True(RouteAddress.SamePath("/about", other), other);

        Assert.Equal("orders/42", RouteAddress.PathOf("/orders/42/?q=1"));
        Assert.Equal(string.Empty, RouteAddress.PathOf(null));
        Assert.Equal(string.Empty, RouteAddress.PathOf("/"));
    }

    [Fact]
    public void The_words_of_a_path_ignore_the_case_and_other_paths_differ()
    {
        Assert.True(RouteAddress.SamePath("/Orders", "/orders"));
        Assert.False(RouteAddress.SamePath("/orders", "/order"));
        Assert.False(RouteAddress.SamePath(null, "/orders"));
        Assert.False(RouteAddress.SamePath("/orders", null));
    }

    [Fact]
    public void An_address_is_the_same_whatever_its_slashes_escapes_and_order_of_query()
    {
        Assert.True(RouteAddress.Same("about", "/about/"));
        Assert.True(RouteAddress.Same("/dogs/Afghan Hound", "/dogs/Afghan%20Hound"));
        Assert.True(RouteAddress.Same("/s?x=1&y=2", "/s?Y=2&x=1"));
        Assert.False(RouteAddress.Same("/s?x=1", "/s?x=2"));
        Assert.False(RouteAddress.Same("/s?q=ABC", "/s?q=abc"));
        Assert.False(RouteAddress.Same("/s?path=a/", "/s?path=a"));
        Assert.False(RouteAddress.Same("/files/a%2Fb", "/files/a/b"));
        Assert.False(RouteAddress.Same(null, "/s"));
        Assert.False(RouteAddress.Same("/s", null));
    }

    [Fact]
    public void Nothing_shown_is_the_empty_address_and_not_any_other()
    {
        Assert.True(RouteAddress.Same(string.Empty, string.Empty));
        Assert.True(RouteAddress.Same(string.Empty, "/"));
        Assert.False(RouteAddress.Same(string.Empty, "orders"));
    }

    [Fact]
    public void The_query_tells_two_entries_of_the_same_path_apart()
        => Assert.False(RouteAddress.Same("/items?kind=a", "/items?kind=b"));

    [Fact]
    public void The_case_of_a_value_counts_only_when_the_template_says_it_is_a_value()
    {
        static string? Items(string path) => "/items/{id}";

        Assert.False(RouteAddress.Same("/items/aB", "/items/Ab", Items));
        Assert.True(RouteAddress.Same("/ITEMS/aB", "/items/aB", Items));
        Assert.True(RouteAddress.Same("/Whatever", "/whatever", _ => null));
        Assert.True(RouteAddress.Same("/Whatever", "/whatever"));
    }

    [Fact]
    public void A_protocol_relative_address_stays_inside_the_application()
    {
        Assert.Equal("/host", RouteAddress.Rooted("//host"));
        Assert.Equal("/host", RouteAddress.Rooted("/\\host"));
        Assert.Equal("/about", RouteAddress.Rooted("about"));
    }

    public sealed class RaItemView : IRouteableFor { public object? Context { get; set; } }
    public sealed class RaItemVm;

    [Fact]
    public void The_router_tells_the_words_of_a_path_from_its_values_and_a_router_that_does_not_know_its_templates_cannot()
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRoute<RaItemView, RaItemVm>(ServiceLifetime.Transient, ServiceLifetime.Transient, "/ra-items/{id}");
        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<IRouter>();

        Assert.False(RouteAddress.SameFor(router, "/ra-items/aB", "/ra-items/Ab"));     // two identifiers
        Assert.True(RouteAddress.SameFor(router, "/RA-items/ab", "/ra-items/ab"));      // a word that changed its case
        Assert.True(RouteAddress.SameFor(Substitute.For<IRouter>(), "/ra-items/aB", "/ra-items/Ab"));
    }
}
