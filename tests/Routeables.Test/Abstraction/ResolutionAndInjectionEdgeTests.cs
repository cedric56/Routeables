using Routeables.Abstraction;
using Routeables.Abstraction.Injections;
using Routeables.Abstraction.Registrations;
using Routeables.Abstraction.Routing;
using Routeables.Routing;
using Routeables.Tests.Pages;

namespace Routeables.Test.Abstraction;

/// <summary>The edges of the resolution of views and of the injection of their properties.</summary>
public class ResolutionAndInjectionEdgeTests
{
    [AttributeUsage(AttributeTargets.Property)]
    private sealed class ParamAttribute : Attribute;

    private static IServiceProvider Provider() => new ServiceCollection()
        .AddSingleton<IRouteableInjection>(new RouteablePropertyInjection(new RouteablePropertyEntry(typeof(ParamAttribute))))
        .BuildServiceProvider();

    private sealed class BagTarget { public RouteableParameters? Bag { get; set; } }

    private sealed class ViewTarget
    {
        [Param] public string? Tab { get; set; }
        [Param] public object? Context { get; set; }
        public RouteableParameters? Bag { get; set; }
        [Param] public string? Other { get; set; }
    }

    [Fact]
    public void A_property_that_holds_the_whole_bag_receives_every_value()
    {
        var target = new BagTarget().Inject(Provider(), new Dictionary<string, object?> { ["Tab"] = "x", ["Page"] = 2 });

        Assert.NotNull(target.Bag);
        Assert.Equal("x", target.Bag!["tab"]);
        Assert.Equal(2, target.Bag["page"]);
    }

    [Fact]
    public void A_value_given_to_the_view_model_reaches_the_view_property_of_that_name_only()
    {
        var args = RouteableArgs.Create()
            .WithViewModel("tab", "from the address", attribute: true)
            .WithViewModel("context", "not for the context", attribute: true)
            .WithViewModel("nobody", "no property", attribute: true);

        var target = new ViewTarget().InjectView(Provider(), args);

        Assert.Equal("from the address", target.Tab);
        Assert.Null(target.Context);
        Assert.Null(target.Other);
    }

    [Fact]
    public void An_argument_is_read_by_its_position_among_those_that_have_a_value()
    {
        IList<RouteableArgument> args =
        [
            new RouteableArgument(null),
            new RouteableArgument("named", "n", attribute: true),
            new RouteableArgument(5),
            new RouteableArgument("six"),
        ];

        Assert.Equal(5, args.Resolve<int>(0));
        Assert.Equal("six", args.Resolve<string>(1));
    }

    [Fact]
    public void An_argument_out_of_range_or_of_another_type_is_refused()
    {
        IList<RouteableArgument> args = [new RouteableArgument(5)];

        Assert.Throws<ArgumentException>(() => args.Resolve<int>(1));
        Assert.Throws<ArgumentException>(() => args.Resolve<int>(-1));
        Assert.Throws<InvalidCastException>(() => args.Resolve<string>(0));
    }

    private static ServiceProvider RoutedProvider(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        services.RegisterRouting();
        services.RegisterRouteableArgsFactory(() => RouteableArgs.Create()); // what the route builder adds when it is used
        register(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void The_view_of_a_view_model_is_resolved_by_the_name_of_the_view_model()
    {
        using var provider = RoutedProvider(s => s.RegisterRoute<FakeViewFor, FakeViewModel>());

        Assert.IsType<FakeViewFor>(provider.GetContextFor<FakeViewModel>());
    }

    [Fact]
    public void Content_is_created_by_its_key_with_the_context_as_the_view_parameter()
    {
        object? seen = null;
        using var provider = RoutedProvider(s => s.RegisterRoute("content", (_, args) =>
        {
            seen = args.GetKeyForContent();
            return "made";
        }));
        var context = new object();

        var content = provider.GetContent("content", context);

        Assert.Equal("made", content);
        Assert.Same(context, seen);
    }

    [Fact]
    public void Content_that_is_not_registered_is_refused()
    {
        using var provider = RoutedProvider(_ => { });

        var error = Assert.Throws<InvalidOperationException>(() => provider.GetContent("missing", new object()));

        Assert.Contains("missing", error.Message);
    }
}
