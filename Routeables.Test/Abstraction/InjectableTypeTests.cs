using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;

namespace Routeables.Tests.Abstraction;

public class InjectableTypeTests
{
    [AttributeUsage(AttributeTargets.Property)]
    private sealed class MarkAttribute : Attribute;

    private class Base
    {
        [Mark] public virtual string? Inherited { get; set; }
    }

    private class Sample : Base
    {
        [Mark] public string? Plain { get; set; }
        public string? Unmarked { get; set; }
        public string? ReadOnly { get; } = "ro";
        public override string? Inherited { get; set; }
    }

    [Fact]
    public void Reflect_lists_the_writable_properties_with_their_attributes()
    {
        var type = InjectableType.Reflect<Sample>();
        var names = type.Properties.Select(p => p.Name).ToList();

        Assert.Contains("Plain", names);
        Assert.Contains("Unmarked", names);
        Assert.DoesNotContain("ReadOnly", names);
        Assert.True(type.Properties.Single(p => p.Name == "Plain").Has(typeof(MarkAttribute)));
        Assert.False(type.Properties.Single(p => p.Name == "Unmarked").Has(typeof(MarkAttribute)));
    }

    [Fact]
    public void An_override_does_not_inherit_the_attributes_of_the_property_it_overrides()
    {
        var property = InjectableType.Reflect<Sample>().Properties.Single(p => p.Name == "Inherited");
        Assert.False(property.Has(typeof(MarkAttribute)));
    }

    [Fact]
    public void Reflect_is_cached()
        => Assert.Same(InjectableType.Reflect<Sample>(), InjectableType.Reflect<Sample>());

    [Fact]
    public void A_property_is_set_and_read_through_its_accessors()
    {
        var sample = new Sample();
        var property = InjectableType.Reflect<Sample>().Properties.Single(p => p.Name == "Plain");

        property.Set(sample, "x");

        Assert.Equal("x", sample.Plain);
        Assert.Equal("x", property.Get!(sample));
    }

    private sealed class Derived : Sample;

    [Fact]
    public void Resolve_reads_the_static_type_when_it_is_the_exact_type()
    {
        var provider = new ServiceCollection().BuildServiceProvider();

        var type = InjectionTable.Resolve<Sample>(provider, new Sample());

        Assert.Contains(type.Properties, p => p.Name == "Plain");
    }

    [Fact]
    public void Resolve_refuses_an_undescribed_derived_type_instead_of_half_filling_it()
    {
        var provider = new ServiceCollection().BuildServiceProvider();

        var error = Assert.Throws<InvalidOperationException>(() => InjectionTable.Resolve<Sample>(provider, new Derived()));

        Assert.Contains("not described", error.Message);
    }

    [Fact]
    public void Resolve_uses_the_description_of_the_exact_type_first()
    {
        var table = new InjectionTable();
        var described = new InjectableType([], []);
        table.Add(typeof(Derived), described);
        var provider = new ServiceCollection().AddSingleton(table).BuildServiceProvider();

        Assert.Same(described, InjectionTable.Resolve<Sample>(provider, new Derived()));
    }
}
