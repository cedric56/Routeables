using Microsoft.AspNetCore.Components.Rendering;
using Routeables.Blazor;

namespace Routeables.Tests.Razor;

/// <summary>The activator that gives Blazor the view the router created, once, and builds everything else as Blazor does.</summary>
public class ComponentActivatorTests
{
    public sealed class PageViewModel;

    public sealed class PageView : RouteableFor<PageView, PageViewModel>;

    public sealed class OtherView : RouteableFor<OtherView, PageViewModel>;

    public sealed class FailingView : Microsoft.AspNetCore.Components.ComponentBase
    {
        public FailingView() => throw new InvalidOperationException("constructor failed");
    }

    public sealed class NoDefaultConstructorView : Microsoft.AspNetCore.Components.ComponentBase
    {
        public NoDefaultConstructorView(int value) => Value = value;

        public int Value { get; }
    }

    [Fact]
    public void An_exception_of_a_constructor_is_not_wrapped()
    {
        var activator = new RouteableComponentActivator();

        var exception = Assert.Throws<InvalidOperationException>(() => activator.CreateInstance(typeof(FailingView)));

        Assert.Equal("constructor failed", exception.Message);
    }

    [Fact]
    public void A_component_without_a_public_default_constructor_says_which_one()
    {
        var activator = new RouteableComponentActivator();

        var exception = Assert.Throws<InvalidOperationException>(() => activator.CreateInstance(typeof(NoDefaultConstructorView)));

        Assert.Contains(nameof(NoDefaultConstructorView), exception.Message);
    }

    [Fact]
    public void A_type_that_is_not_a_component_is_refused()
    {
        var activator = new RouteableComponentActivator();

        Assert.Throws<ArgumentException>(() => activator.CreateInstance(typeof(string)));
    }

    [Fact]
    public void An_offered_view_is_the_component_blazor_gets()
    {
        var activator = new RouteableComponentActivator();
        var view = new PageView();
        activator.Offer(view);

        var component = activator.CreateInstance(typeof(PageView));

        Assert.Same(view, component);
        Assert.True(view.Claimed);
    }

    [Fact]
    public void A_view_is_handed_over_only_once()
    {
        var activator = new RouteableComponentActivator();
        var view = new PageView();
        activator.Offer(view);
        activator.CreateInstance(typeof(PageView));

        var second = activator.CreateInstance(typeof(PageView));

        Assert.NotSame(view, second);
        Assert.IsType<PageView>(second);
    }

    [Fact]
    public void A_view_that_was_already_rendered_is_not_offered_again()
    {
        // A component has one render handle for life: shown again (Back), the router's view cannot go to a renderer a second time.
        var activator = new RouteableComponentActivator();
        var view = new PageView { Claimed = true };

        activator.Offer(view);

        Assert.NotSame(view, activator.CreateInstance(typeof(PageView)));
    }

    [Fact]
    public void Another_type_is_built_normally_and_the_offer_waits()
    {
        var activator = new RouteableComponentActivator();
        var view = new PageView();
        activator.Offer(view);

        var other = activator.CreateInstance(typeof(OtherView));

        Assert.IsType<OtherView>(other);
        Assert.False(view.Claimed);
        Assert.Same(view, activator.CreateInstance(typeof(PageView)));
    }

    [Fact]
    public void Two_views_of_one_type_are_handed_over_in_the_order_they_were_offered()
    {
        var activator = new RouteableComponentActivator();
        var first = new PageView();
        var second = new PageView();
        activator.Offer(first);
        activator.Offer(second);

        Assert.Same(first, activator.CreateInstance(typeof(PageView)));
        Assert.Same(second, activator.CreateInstance(typeof(PageView)));
    }

    [Fact]
    public void A_view_that_is_disposed_before_it_reaches_the_screen_is_withdrawn()
    {
        var activator = new RouteableComponentActivator();
        var view = new PageView { ComponentActivator = activator };
        activator.Offer(view);

        view.Dispose();

        Assert.NotSame(view, activator.CreateInstance(typeof(PageView)));
    }

    [Fact]
    public void Rendering_a_view_offers_it()
    {
        var activator = new RouteableComponentActivator();
        var view = new PageView { ComponentActivator = activator };

        view.Render()!(new RenderTreeBuilder());

        Assert.Same(view, activator.CreateInstance(typeof(PageView)));
    }

    [Fact]
    public void A_view_without_an_activator_renders_as_before()
    {
        var view = new PageView();

        var exception = Record.Exception(() => view.Render()!(new RenderTreeBuilder()));

        Assert.Null(exception);
    }
}
