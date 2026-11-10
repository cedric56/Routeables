using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Routeables.AspNetCore;
using Routeables.Routing;
using Routeables.Tests.Core;
using System.Text.Encodings.Web;

namespace Routeables.Tests.Hosts;

/// <summary><c>&lt;routeable-menu&gt;</c>: the menu of the ASP.NET Core host, one link per entry of the menu view model.</summary>
public class RouteableMenuTagHelperTests
{
    private sealed class Cats;
    private sealed class Dogs;

    private static string Render(FakeMenuViewModel main, string path, Action<RouteableMenuTagHelper>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<Routeables.Abstraction.Bases.IRouteableMenuViewModel>(main);
        using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        http.Request.Path = path;

        var helper = new RouteableMenuTagHelper { ViewContext = new ViewContext { HttpContext = http } };
        configure?.Invoke(helper);
        var context = new TagHelperContext("routeable-menu", [], new Dictionary<object, object>(), "id");
        var output = new TagHelperOutput("routeable-menu", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        helper.Process(context, output);

        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    private static FakeMenuViewModel Menu()
    {
        var main = new FakeMenuViewModel();
        main.Entries.Add(new RouteableMenuEntry<Cats>("Cats", null, "/cats", "/cats", "All the cats"));
        main.Entries.Add(new RouteableMenuEntry<Dogs>("Dogs", null, "dogs", "/dogs"));
        return main;
    }

    [Fact]
    public void It_links_each_entry_and_marks_the_first_segment_of_the_address()
    {
        var html = Render(Menu(), "/cats/Tom");

        Assert.StartsWith("<nav", html);
        Assert.Contains("href=\"/cats\"", html);
        Assert.Contains("href=\"/dogs\"", html);
        Assert.Contains("All the cats", html);
        Assert.Single(html.Split("aria-current=\"page\"").Skip(1));
        Assert.Contains("rt-menu-active", html);
    }

    [Fact]
    public void A_locked_entry_has_no_link()
    {
        var main = Menu();
        main.Entries[1].IsLock = true;

        var html = Render(main, "/cats");

        Assert.DoesNotContain("href=\"/dogs\"", html);
        Assert.Contains("rt-menu-locked", html);
        Assert.Contains("href=\"/cats\"", html);
    }

    [Fact]
    public void The_entries_and_the_current_address_can_be_given()
    {
        var main = Menu();

        var html = Render(main, "/cats", h =>
        {
            h.Entries = [main.Entries[1]];
            h.Current = "/dogs";
        });

        Assert.DoesNotContain("Cats", html);
        Assert.Contains("rt-menu-active", html);
    }
}
