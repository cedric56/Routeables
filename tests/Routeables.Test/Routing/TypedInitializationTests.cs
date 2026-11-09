using Routeables.Routing;

namespace Routeables.Tests.Routing;

/// <summary>The carrier of the typed data of <c>GoToRouteAsync&lt;TViewModel, TData&gt;</c>: it travels in the values, never in the view model parameters.</summary>
public class TypedInitializationTests
{
    [Fact]
    public void The_data_travel_in_the_values_under_a_fixed_key_and_not_in_the_view_model_parameters()
    {
        var args = TypedInitialization.CreateArgs(42);

        var carrier = Assert.Single(args.Values);
        Assert.Equal(TypedInitialization.Key, carrier.name);
        Assert.IsType<RouteableTypedInitializer<int>>(carrier.value);
        Assert.Empty(args.ViewModelParameters);
        Assert.Empty(args.ViewParameters);
    }

    [Fact]
    public void The_carrier_is_found_again_in_the_arguments()
    {
        var args = TypedInitialization.CreateArgs("data");

        var found = TypedInitialization.Find(args);

        Assert.NotNull(found);
        Assert.IsType<RouteableTypedInitializer<string>>(found);
    }

    [Fact]
    public void Arguments_without_a_carrier_give_nothing()
    {
        var args = RouteableArgs.Create().WithValue("other", 1);

        Assert.Null(TypedInitialization.Find(args));
        Assert.Null(TypedInitialization.Find(RouteableArgs.Create()));
    }

    [Fact]
    public void Only_a_typed_initializer_is_taken_for_the_carrier()
    {
        var args = RouteableArgs.Create().WithValue(TypedInitialization.Key, "not an initializer");

        Assert.Null(TypedInitialization.Find(args));
    }
}
