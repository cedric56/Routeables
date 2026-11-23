using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Routeables.Abstraction;
using Routeables.Generated;
using System.Text;

namespace Routeables.Tests.Generated;

/// <summary>
/// What the registration generator reads of a type, tested on its own from compilations: the constructor to call (and when there is none to name),
/// the defaults of its parameters as C# literals, the attributes of the type and of its properties, and the properties the injection could write.
/// A type that cannot be described completely is left to reflection: <c>CanConstruct</c> or <c>CanDescribe</c> is false.
/// </summary>
public class RouteBuildsTests
{
    private const string Prelude = """
        #nullable enable
        using System;
        using Microsoft.Extensions.DependencyInjection;
        using Routeables.Abstraction;
        using Routeables.Abstraction.Routing;

        public enum Color { Red, Green, Blue = 2 }
        public class CustomAttribute : Attribute
        {
            public CustomAttribute() { }
            public CustomAttribute(int x) { }
            public string? Name { get; set; }
        }
        """;

    private static IEnumerable<MetadataReference> References() => AppDomain.CurrentDomain.GetAssemblies()
        .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
        .Select(assembly => assembly.Location)
        .Concat([
            typeof(RouteableAttribute).Assembly.Location,
            typeof(IServiceCollection).Assembly.Location,
            typeof(ActivatorUtilitiesConstructorAttribute).Assembly.Location,
        ])
        .Distinct()
        .Select(location => (MetadataReference)MetadataReference.CreateFromFile(location));

    private static (Compilation Compilation, INamedTypeSymbol Type) Compile(string source, string typeName)
    {
        var compilation = CSharpCompilation.Create(
            "RouteBuildsTests.Dynamic",
            [CSharpSyntaxTree.ParseText(Prelude + "\n" + source)],
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var type = compilation.GetTypeByMetadataName(typeName) ?? throw new InvalidOperationException("No type " + typeName);
        return (compilation, type);
    }

    private static RouteBuilds.BuildInfo Analyze(string source, string typeName = "T")
    {
        var (compilation, type) = Compile(source, typeName);
        return RouteBuilds.Analyze(compilation, type);
    }

    // ---- the constructor

    [Fact]
    public void A_class_with_one_public_constructor_is_built_with_its_parameters_and_their_defaults()
    {
        var info = Analyze("public class T { public T(int a, string b = \"x\") { } }");

        Assert.True(info.CanConstruct);
        Assert.Equal("global::T", info.TypeName);
        Assert.Equal(2, info.Parameters.Count);
        Assert.Equal("a", info.Parameters[0].Name);
        Assert.False(info.Parameters[0].HasDefault);
        Assert.True(info.Parameters[1].HasDefault);
        Assert.Equal("\"x\"", info.Parameters[1].Default);
    }

    [Fact]
    public void The_constructor_marked_for_the_activator_is_the_one_called_among_several()
    {
        var info = Analyze("""
            public class T
            {
                public T() { }
                [ActivatorUtilitiesConstructor] public T(int chosen) { }
                public T(string other) { }
            }
            """);

        Assert.True(info.CanConstruct);
        Assert.Equal("chosen", Assert.Single(info.Parameters.ToArray()).Name);
    }

    [Theory]
    [InlineData("public class T { public T() { } public T(int a) { } }")]                       // several, none marked: chosen at run time
    [InlineData("public class T { internal T() { } }")]                                          // none public
    [InlineData("public abstract class T { public T() { } }")]
    [InlineData("public class T { public required int P { get; set; } public T() { } }")]
    [InlineData("public class T<X> { public T() { } }")]
    [InlineData("public class T { public T(ref int a) { } }")]
    [InlineData("public class T { public T(params int[] a) { } }")]
    [InlineData("public class T { public T([System.Diagnostics.CodeAnalysis.NotNull] string a) { } }")]
    [InlineData("public class T { public T(Span<int> a) { } }")]
    [InlineData("public unsafe class T { public T(int* a) { } }")]
    [InlineData("public class T { public T(dynamic a) { } }")]
    public void A_constructor_that_cannot_be_called_from_generated_code_is_left_to_reflection(string source)
    {
        var info = Analyze(source, source.Contains("T<X>") ? "T`1" : "T");

        Assert.False(info.CanConstruct);
        Assert.Equal(0, info.Parameters.Count);
        Assert.False(info.TakesRouter);
    }

    [Fact]
    public void A_class_inside_a_generic_class_is_left_to_reflection_whole()
    {
        var info = Analyze("public class Outer<X> { public class T { public T() { } } }", "Outer`1+T");

        Assert.False(info.CanConstruct);
        Assert.False(info.CanDescribe);
    }

    [Fact]
    public void A_constructor_that_takes_the_router_says_so()
    {
        var info = Analyze("public class T { public T(IRouter router, int a) { } }");

        Assert.True(info.CanConstruct);
        Assert.True(info.TakesRouter);
    }

    [Theory]
    [InlineData("string? s = null", "default!")]
    [InlineData("int i = -5", "-5")]
    [InlineData("bool b = true", "true")]
    [InlineData("char c = 'x'", "'x'")]
    [InlineData("long l = 7", "(7L)")]
    [InlineData("long l = long.MinValue", "long.MinValue")]
    [InlineData("float f = 1.5f", "(1.5f)")]
    [InlineData("float f = float.NaN", "float.NaN")]
    [InlineData("float f = float.PositiveInfinity", "float.PositiveInfinity")]
    [InlineData("float f = float.NegativeInfinity", "float.NegativeInfinity")]
    [InlineData("double d = 2.5", "(2.5d)")]
    [InlineData("double d = double.NaN", "double.NaN")]
    [InlineData("double d = double.PositiveInfinity", "double.PositiveInfinity")]
    [InlineData("double d = double.NegativeInfinity", "double.NegativeInfinity")]
    [InlineData("decimal m = 9.99m", "(9.99m)")]
    [InlineData("Color c = Color.Blue", "(global::Color)(2)")]
    [InlineData("Color c = (Color)(-1)", "(global::Color)(-1)")]
    [InlineData("Color? c = Color.Green", "(global::Color)(1)")]
    public void The_default_of_a_parameter_is_written_as_a_literal_of_its_own_type(string parameter, string expected)
    {
        var info = Analyze($"public class T {{ public T({parameter}) {{ }} }}");

        Assert.True(info.CanConstruct);
        Assert.Equal(expected, info.Parameters[0].Default);
    }

    // ---- the attributes of the type

    [Fact]
    public void The_attributes_of_a_type_are_rebuilt_as_expressions_with_their_arguments()
    {
        var info = Analyze("[Custom(1, Name = \"n\")] public class T { }");

        Assert.True(info.CanDescribe);
        Assert.Equal("new global::CustomAttribute(1) { Name = \"n\" }", Assert.Single(info.TypeAttributes.ToArray()));
    }

    [Fact]
    public void The_attributes_of_the_base_library_are_not_kept()
    {
        var info = Analyze("[Obsolete] [Custom] public class T { }");

        Assert.Equal("new global::CustomAttribute()", Assert.Single(info.TypeAttributes.ToArray()));
    }

    [Fact]
    public void An_inherited_attribute_of_a_base_type_is_kept_and_one_that_is_not_inherited_is_not()
    {
        var inherited = Analyze("[Custom(1)] public class B { } public class T : B { }");
        var notInherited = Analyze("""
            [AttributeUsage(AttributeTargets.Class, Inherited = false)] public class LocalAttribute : Attribute { }
            [Local] public class B { }
            public class T : B { }
            """);
        var usageWithoutInherited = Analyze("""
            [AttributeUsage(AttributeTargets.Class)] public class KeptAttribute : Attribute { }
            [Kept] public class B { }
            public class T : B { }
            """);

        Assert.Equal("new global::CustomAttribute(1)", Assert.Single(inherited.TypeAttributes.ToArray()));
        Assert.Equal(0, notInherited.TypeAttributes.Count);
        Assert.Equal("new global::KeptAttribute()", Assert.Single(usageWithoutInherited.TypeAttributes.ToArray()));
    }

    [Fact]
    public void An_attribute_the_generated_code_could_not_write_leaves_the_type_to_reflection()
    {
        var info = Analyze("""
            public class Holder
            {
                [Hidden] public class T { }
                private class HiddenAttribute : Attribute { }
            }
            """, "Holder+T");

        Assert.False(info.CanDescribe);
        Assert.Equal(0, info.TypeAttributes.Count);
        Assert.Equal(0, info.Properties.Count);
    }

    [Fact]
    public void An_attribute_whose_argument_does_not_compile_leaves_the_type_to_reflection()
        => Assert.False(Analyze("[Custom(Missing.Value)] public class T { }").CanDescribe);

    [Fact]
    public void A_type_with_a_base_from_another_assembly_is_left_to_reflection()
        => Assert.False(Analyze("public class T : Exception { }").CanDescribe);

    // ---- the properties

    [Fact]
    public void A_property_with_an_attribute_is_described_with_the_names_of_its_attributes()
    {
        var info = Analyze("""
            public class T
            {
                [Custom] public string? Marked { get; set; }
                public string? Plain { get; set; }
                public string? ReadOnly { get; }
                public static string? Shared { get; set; }
                public string this[int i] { get => ""; set { } }
            }
            """);

        var property = Assert.Single(info.Properties.ToArray());
        Assert.Equal("Marked", property.Name);
        Assert.Equal("CustomAttribute", Assert.Single(property.Attributes.ToArray()));
        Assert.True(property.CanGet);
        Assert.False(property.Bag);
    }

    [Fact]
    public void The_names_of_an_attribute_are_its_nested_name_and_those_of_its_base_types()
    {
        var info = Analyze("""
            public class BaseAttribute : Attribute { }
            public class Outer { public class InnerAttribute : BaseAttribute { } }
            public class T { [Outer.Inner] public string? P { get; set; } }
            """);

        var names = Assert.Single(info.Properties.ToArray()).Attributes.ToArray();
        Assert.Equal(new[] { "BaseAttribute", "Outer+InnerAttribute" }, names);
    }

    [Fact]
    public void A_property_that_holds_the_parameters_of_the_route_is_described_as_a_bag()
    {
        var info = Analyze("public class T { public RouteableParameters? Bag { get; set; } }");

        var property = Assert.Single(info.Properties.ToArray());
        Assert.True(property.Bag);
        Assert.Equal(0, property.Attributes.Count);
    }

    [Fact]
    public void When_the_type_names_properties_through_its_own_attributes_every_writable_property_is_described()
    {
        var info = Analyze("[Custom] public class T { public int A { get; set; } public int B { get; set; } }");

        Assert.Equal(new[] { "A", "B" }, info.Properties.ToArray().Select(p => p.Name).Order().ToArray());
    }

    [Theory]
    [InlineData("[Custom] public int P { get; init; }")]                  // init only: not writable from generated code
    [InlineData("[Custom] public int P { get; private set; }")]           // a setter that is not accessible
    [InlineData("[Custom] public int P { private get; set; }")]           // a getter that is not accessible
    [InlineData("[Custom] public Span<int> P { get => default; set { } }")]       // a type the code cannot hold
    public void A_property_the_injection_could_write_but_the_generated_code_cannot_leaves_the_type_to_reflection(string property)
    {
        var info = Analyze($"public class T {{ {property} }}");

        Assert.False(info.CanDescribe);
        Assert.Equal(0, info.Properties.Count);
    }

    [Fact]
    public void A_property_that_hides_another_leaves_the_type_to_reflection_and_an_override_does_not()
    {
        var hidden = Analyze("public class B { [Custom] public int P { get; set; } } public class T : B { [Custom] public new int P { get; set; } }");
        // The override does not see the attributes of the property it overrides (reflection does not either): without its own it is not described.
        var overridden = Analyze("public class B { [Custom] public virtual int P { get; set; } } public class T : B { public override int P { get; set; } }");
        var overriddenAndMarked = Analyze("public class B { [Custom] public virtual int P { get; set; } } public class T : B { [Custom] public override int P { get; set; } }");

        Assert.False(hidden.CanDescribe);
        Assert.True(overridden.CanDescribe);
        Assert.Equal(0, overridden.Properties.Count);
        Assert.True(overriddenAndMarked.CanDescribe);
        Assert.Equal("P", Assert.Single(overriddenAndMarked.Properties.ToArray()).Name);
    }

    [Fact]
    public void A_private_property_of_a_base_type_is_not_read()
    {
        var info = Analyze("public class B { [Custom] private int Hidden { get; set; } [Custom] public int Seen { get; set; } } public class T : B { }");

        Assert.Equal("Seen", Assert.Single(info.Properties.ToArray()).Name);
    }

    // ---- the code that is written

    [Fact]
    public void The_factory_of_a_view_model_gives_its_arguments_by_name_and_says_whether_it_takes_the_router()
    {
        var info = Analyze("public class T { public T(IRouter router, int count = 3) { } }");
        var sb = new StringBuilder();

        RouteBuilds.EmitFactory(sb, "BuildT", info, viewModel: true);

        var code = sb.ToString();
        Assert.Contains("private static global::T BuildT(global::System.IServiceProvider sp, global::Routeables.Abstraction.Routing.IRouteableArgs args)", code);
        Assert.Contains("ForViewModel(sp, args, \"T\", new string[] { \"router\", \"count\" }, true)", code);
        Assert.Contains("c.Next<int>(\"count\", 3)", code);
        Assert.Contains("InjectViewModel(new global::T(", code);
    }

    [Fact]
    public void The_factory_of_a_view_model_without_parameters_has_no_arguments_to_read()
    {
        var info = Analyze("public class T { }");
        var sb = new StringBuilder();

        RouteBuilds.EmitFactory(sb, "BuildT", info, viewModel: true);

        Assert.DoesNotContain("ForViewModel", sb.ToString());
        Assert.Contains("InjectViewModel(new global::T()", sb.ToString());
    }

    [Fact]
    public void The_factory_of_a_view_reads_its_arguments_from_the_view_and_the_one_without_parameters_does_not()
    {
        var with = new StringBuilder();
        var without = new StringBuilder();

        RouteBuilds.EmitFactory(with, "BuildV", Analyze("public class T { public T(int a) { } }"), viewModel: false);
        RouteBuilds.EmitFactory(without, "BuildV", Analyze("public class T { }"), viewModel: false);

        Assert.Contains("ConstructorArguments.ForView(sp, args, \"T\")", with.ToString());
        Assert.Contains("InjectView(new global::T(c.Next<int>(\"a\"))", with.ToString());
        Assert.DoesNotContain("ForView(", without.ToString());
        Assert.Contains("InjectView(new global::T()", without.ToString());
    }

    [Fact]
    public void The_description_lists_the_attributes_of_the_type_and_the_properties_with_their_getter()
    {
        var info = Analyze("""
            [Custom(1)]
            public class T
            {
                [Custom] public string? Name { get; set; }
                [Custom] public string? Only { set { } }
                [Custom] public string @event { get; set; } = "";
            }
            """);
        var sb = new StringBuilder();

        RouteBuilds.EmitDescription(sb, "Describe", info);

        var code = sb.ToString();
        Assert.Contains("new global::System.Attribute[] { new global::CustomAttribute(1) }", code);
        Assert.Contains("new(\"Name\", typeof(string), new string[] { \"CustomAttribute\" },", code);
        Assert.Contains("o => ((global::T)o).Name", code);
        Assert.Contains("null),", code);                        // a property that cannot be read has no getter
        Assert.Contains("((global::T)o).@event = v is null ? default! : (string)v", code);
    }

    [Fact]
    public void The_description_of_a_type_without_attributes_or_properties_is_empty()
    {
        var sb = new StringBuilder();

        RouteBuilds.EmitDescription(sb, "Describe", Analyze("public class T { }"));

        Assert.Contains("global::System.Array.Empty<global::System.Attribute>()", sb.ToString());
    }
}
