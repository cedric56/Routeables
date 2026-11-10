using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction.Bases;

namespace Routeables.AspNetCore;

/// <summary>
/// The menu of the application: a <c>nav</c> with one link per entry of the menu view model (<see cref="IRouteableMenuViewModel.Entries"/>), the
/// entry of the current address marked, an entry that a page locked shown without a link. It is the <c>RouteableMenu</c> of the other platforms.
/// <code>
/// @addTagHelper *, Routeables.AspNetCore      &lt;!-- once, in _ViewImports.cshtml --&gt;
///
/// &lt;routeable-menu class="drawer" /&gt;
/// &lt;routeable-menu entries="Model.MenuEntries" current="@Model.Url" /&gt;
/// </code>
/// The classes are <c>rt-menu-link</c> (each entry), <c>rt-menu-active</c> (the current one), <c>rt-menu-locked</c>, <c>rt-menu-icon</c>,
/// <c>rt-menu-text</c>, <c>rt-menu-name</c> and <c>rt-menu-description</c>; style them, and a <c>class</c> on the tag goes to the <c>nav</c>.
/// </summary>
[HtmlTargetElement("routeable-menu")]
public sealed class RouteableMenuTagHelper : TagHelper
{
    /// <summary>The entries to draw; those of the menu view model when not set (give a list to leave some out).</summary>
    [HtmlAttributeName("entries")]
    public IEnumerable<IRouteableMenuEntry>? Entries { get; set; }

    /// <summary>The address of the page that is shown, which decides the current entry; the path of the request when not set.</summary>
    [HtmlAttributeName("current")]
    public string? Current { get; set; }

    /// <summary>The accessible name of the menu.</summary>
    [HtmlAttributeName("label")]
    public string Label { get; set; } = "Menu";

    /// <summary>The context of the view the tag is in.</summary>
    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var entries = Entries ?? ViewContext.HttpContext.RequestServices.GetService<IRouteableMenuViewModel>()?.Entries ?? [];

        // The menu marks the first segment of the address: /cats/Tom is still in Cats.
        var current = FirstSegment(Current ?? ViewContext.HttpContext.Request.Path.Value);

        output.TagName = "nav";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("aria-label", Label);
        output.Content.Clear();

        foreach (var entry in entries)
        {
            var address = RouteableAddress.Of(entry.Route);
            var href = RouteableAddress.Of(entry.Route, ViewContext.HttpContext.Request.PathBase);
            var active = string.Equals(FirstSegment(address), current, StringComparison.OrdinalIgnoreCase);
            var link = new TagBuilder(entry.IsLock ? "span" : "a");
            link.AddCssClass("rt-menu-link");
            if (entry.IsLock)
            {
                // A page published a lock: the entry cannot be followed until it publishes the unlock.
                link.AddCssClass("rt-menu-locked");
                link.Attributes["aria-disabled"] = "true";
            }
            else
            {
                link.Attributes["href"] = href;
                if (active)
                {
                    link.AddCssClass("rt-menu-active");
                    link.Attributes["aria-current"] = "page";
                }
            }

            if (entry.Icon is string glyph && glyph.Length > 0)
                link.InnerHtml.AppendHtml(Span("rt-menu-icon", glyph, hidden: true));

            var text = new TagBuilder("span");
            text.AddCssClass("rt-menu-text");
            text.InnerHtml.AppendHtml(Span("rt-menu-name", entry.DisplayName));
            if (!string.IsNullOrWhiteSpace(entry.Description))
                text.InnerHtml.AppendHtml(Span("rt-menu-description", entry.Description));
            link.InnerHtml.AppendHtml(text);

            output.Content.AppendHtml(link);
        }
    }

    private static TagBuilder Span(string css, string text, bool hidden = false)
    {
        var span = new TagBuilder("span");
        span.AddCssClass(css);
        if (hidden)
            span.Attributes["aria-hidden"] = "true";
        span.InnerHtml.Append(text);
        return span;
    }

    private static string FirstSegment(string? address)
        => "/" + (address?.Split('?')[0].Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "");
}
