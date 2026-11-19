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

    [Theory]
    [InlineData("/\t/host")]
    [InlineData("\t//host")]
    [InlineData("/\n/host")]
    [InlineData("/\r\n\\host")]
    public void A_tab_or_a_line_break_does_not_hide_the_second_slash_from_the_browser_that_drops_them(string route)
        => Assert.Equal("/host", RouteAddress.Rooted(route));

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

    [Fact]
    public void Addresses_are_compared_as_the_parser_reads_them()
    {
        // A plus in a query is a space; an escaped plus stays a plus.
        Assert.True(RouteAddress.Same("/search?q=a+b", "/search?q=a%20b"));
        Assert.False(RouteAddress.Same("/search?q=a+b", "/search?q=a%2Bb"));
        // The fragment is not part of the route.
        Assert.True(RouteAddress.Same("/about#top", "/about"));
        Assert.True(RouteAddress.SamePath("/about#top", "/about"));
        Assert.True(RouteAddress.Same("/about?x=1#top", "/about?x=1"));
        Assert.Equal("about", RouteAddress.PathOf("/about#top"));
    }

    [Fact]
    public void Absolute_keeps_the_end_of_the_query()
    {
        Assert.Equal("/files?dir=/", RouteAddress.Absolute("/files?dir=/"));
        Assert.Equal("/orders/42?x=1", RouteAddress.Absolute("orders/42/?x=1"));
        Assert.Equal("/orders/42", RouteAddress.Absolute("//orders/42//"));
        Assert.Equal("/", RouteAddress.Absolute(null));
        Assert.Equal("/", RouteAddress.Absolute("///"));
    }

    [Fact]
    public void An_escaped_slash_and_an_escaped_percent_sign_are_two_different_values()
    {
        // "a%2Fb" is the value a/b, "a%252Fb" the value a%2Fb: the parser reads two values, so they are two addresses.
        Assert.False(RouteAddress.Same("/files/a%2Fb", "/files/a%252Fb"));
        Assert.True(RouteAddress.Same("/files/a%2Fb", "/files/a%2fb"));
        Assert.False(RouteAddress.Same("/files/a%2Fb", "/files/a/b"));
        Assert.True(RouteAddress.Same("/files/a%252Fb", "/files/a%252fb"));
    }

    [Fact]
    public void An_escaped_slash_is_not_confused_with_any_character()
    {
        // No text stands for a slash: "%EF%BF%BF" (U+FFFF) is one more value.
        Assert.False(RouteAddress.Same("/files/a%2Fb", "/files/a%EF%BF%BFb"));
        Assert.True(RouteAddress.Same("/files/a%2Fb", "/files/a%2fb"));
        Assert.False(RouteAddress.Same("/files/a%2Fb", "/files/a%252Fb"));
    }

    [Fact]
    public void Empty_segments_do_not_make_another_page()
    {
        // The parser skips them: "/a//b" is "/a/b".
        Assert.True(RouteAddress.Same("/a//b", "/a/b"));
        Assert.True(RouteAddress.Same("/a/b/", "//a/b"));
        Assert.False(RouteAddress.Same("/a//b", "/a/c"));
    }

    private static string? TemplateOfXab(string path)
    {
        var found = RouteParser.BestMatch(new (string Template, Type? ViewModelType)[] { ("/x/{a}/{b}", null) }, path);
        return found.Template;
    }

    [Fact]
    public void The_case_of_a_value_that_holds_an_escaped_question_mark_still_counts()
    {
        Assert.False(RouteAddress.Same("/x/q/B", "/x/q/b", TemplateOfXab));
        Assert.False(RouteAddress.Same("/x/q%3F/B", "/x/q%3F/b", TemplateOfXab));
        Assert.False(RouteAddress.Same("/x/q%23/B", "/x/q%23/b", TemplateOfXab));
    }

    [Fact]
    public void A_slash_and_an_escaped_slash_are_the_same_in_a_query_but_not_in_a_path()
    {
        Assert.True(RouteAddress.Same("/s?dir=a/b", "/s?dir=a%2Fb"));
        Assert.True(RouteAddress.Same("/s?dir=a%2fb", "/s?dir=a/b"));
        Assert.False(RouteAddress.Same("/files/a/b", "/files/a%2Fb"));
    }

    [Fact]
    public void An_escaped_path_and_a_path_with_empty_segments_have_the_same_path()
    {
        Assert.True(RouteAddress.SamePath("/%C3%A0-propos", "/à-propos"));
        Assert.True(RouteAddress.SamePath("/a//b", "/a/b/"));
        Assert.False(RouteAddress.SamePath("/files/a%2Fb", "/files/a/b"));
    }

    [Fact]
    public void The_question_mark_of_an_optional_parameter_does_not_start_the_query()
    {
        Assert.Equal("products/{tab?}", RouteAddress.PathOf("products/{tab?}"));
        Assert.Equal("products/{tab?}", RouteAddress.PathOf("/products/{tab?}?x=1"));
        Assert.True(RouteAddress.SamePath("products/{tab?}", "/Products/{tab?}/"));
    }

    [Fact]
    public void Absolute_keeps_the_question_mark_of_an_optional_parameter_in_the_path()
    {
        Assert.Equal("/products/{tab?}", RouteAddress.Absolute("products/{tab?}/"));
        Assert.Equal("/products/{tab?}?x=1", RouteAddress.Absolute("products/{tab?}/?x=1"));
    }

    [Fact]
    public void The_order_of_the_pairs_of_a_query_does_not_count_after_an_optional_parameter()
    {
        Assert.True(RouteAddress.Same("/p/{tab?}?a=1&b=2", "/p/{tab?}?b=2&a=1"));
        Assert.False(RouteAddress.Same("/p/{tab?}?a=1", "/p/{tab?}?a=2"));
    }

    [Fact]
    public void A_brace_that_nothing_closes_in_a_typed_address_does_not_hide_its_query()
    {
        Assert.True(RouteAddress.Same("/notes/a{b?x=1&y=2", "/notes/a{b?y=2&x=1"));
        Assert.False(RouteAddress.Same("/notes/a{b?x=1", "/notes/a{b?x=2"));
    }
}
