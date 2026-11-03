using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;

namespace Routeables.Test.Abstraction;

public class InjectPropertiesTests
{
    [AttributeUsage(AttributeTargets.Property)]
    private sealed class ParamAttribute : Attribute;

    private sealed class Service;

    private class BaseTarget
    {
        [Param] private Service? Hidden { get; set; }
        public Service? ReadHidden() => Hidden;
    }

    private sealed class Target : BaseTarget
    {
        [Param] public Guid Token { get; set; }
        [Param] public int Count { get; set; }
        [Param] public long Big { get; set; }
        [Param] public string? Name { get; set; }
        [Param] public Service? Injected { get; set; }
        [Param] public string? Unset { get; set; }
        public string? NotMarked { get; set; }
    }

    private static IServiceProvider Provider() => new ServiceCollection()
        .AddSingleton<IInjection>(new PropertyInjection(
            new RouteablePropertyEntry(typeof(ParamAttribute))))
        .AddSingleton<Service>()
        .BuildServiceProvider();

    [Fact]
    public void Unset_properties_are_injected_from_the_container_when_a_service_exists()
    {
        var target = new Target().Inject(Provider());

        Assert.NotNull(target.Injected);
        Assert.Null(target.Unset);   // no service of that type: left alone, without an exception
    }

    [AttributeUsage(AttributeTargets.Property)]
    private sealed class ValueAttribute : Attribute;

    private sealed class ValueTarget
    {
        [Value] public Service? Missing { get; set; }
        [Value] public Service? Given { get; set; }
        [Param] public Service? Injected { get; set; }
    }

    [Fact]
    public void A_value_attribute_never_falls_back_to_the_container()
    {
        var provider = new ServiceCollection()
            .AddSingleton<IInjection>(new PropertyInjection(
                new RouteablePropertyEntry(typeof(ValueAttribute), FromContainer: false),
                new RouteablePropertyEntry(typeof(ParamAttribute))))
            .AddSingleton<Service>()
            .BuildServiceProvider();
        var given = new Service();

        var target = new ValueTarget().Inject(provider, new Dictionary<string, object?> { ["given"] = given });

        Assert.Null(target.Missing);            // a value that the routing did not give stays missing
        Assert.Same(given, target.Given);       // an explicit value is still set
        Assert.NotNull(target.Injected);        // the container attribute keeps resolving from the container
    }

    [Fact]
    public void Properties_without_a_registered_attribute_are_left_alone()
    {
        var target = new Target().Inject(Provider(), new Dictionary<string, object?> { ["notmarked"] = "x" });

        Assert.Null(target.NotMarked);
    }

    [Fact]
    public void Nothing_happens_when_no_attributes_are_registered()
    {
        var provider = new ServiceCollection().AddSingleton<Service>().BuildServiceProvider();

        var target = new Target().Inject(provider);

        Assert.Null(target.Injected);
    }
}
