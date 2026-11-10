using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Routeables.Generated;

namespace Routeables.Tests.Generated;

public class PersistGeneratorTests
{
    [Fact]
    public void A_property_is_read_and_written_by_the_generated_code_without_reflection()
    {
        var result = Run("""
            using Routeables.Abstraction.States;
            public class SearchViewModel
            {
                [Persist] public string Query { get; set; } = "";
                [Persist] public int? Page { get; set; }
                public string NotKept { get; set; } = "";
            }
            """);

        var code = Generated(result);

        Assert.Contains("Register<global::SearchViewModel>(", code);
        Assert.Contains("bag.Capture<global::SearchViewModel, string>(m, \"Query\", static x => x.Query);", code);
        Assert.Contains("bag.Restore<global::SearchViewModel, int?>(m, \"Page\", static (x, v) => x.Page = v);", code);
        Assert.DoesNotContain("NotKept", code);
        Assert.DoesNotContain("typeof(", code);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData("[Persist] public static string S { get; set; } = \"\";", "static")]
    [InlineData("[Persist] public string R { get; } = \"\";", "getter and a setter")]
    [InlineData("[Persist] public string I { get; init; } = \"\";", "init-only")]
    [InlineData("[Persist] private string P { get; set; } = \"\";", "public or internal")]
    public void A_property_that_cannot_be_saved_is_reported_and_left_out(string member, string reason)
    {
        var result = Run($$"""
            using Routeables.Abstraction.States;
            public class Vm
            {
                {{member}}
                [Persist] public int Kept { get; set; }
            }
            """);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("ROUT401", diagnostic.Id);
        Assert.Contains(reason, diagnostic.GetMessage());
        Assert.Contains("\"Kept\"", Generated(result));
    }

    [Fact]
    public void A_converter_given_to_the_property_is_built_and_passed_to_the_save_and_the_read()
    {
        var result = Run("""
            using Routeables.Abstraction.States;
            public record Draft(string Text);
            public class DraftConverter : IPersistConverter<Draft>
            {
                public string? Write(Draft value) => value.Text;
                public bool TryRead(string text, out Draft value) { value = new Draft(text); return true; }
            }
            public class Vm { [Persist(Converter = typeof(DraftConverter))] public Draft? Note { get; set; } }
            """);

        var code = Generated(result);

        Assert.Contains("static x => x.Note, new global::DraftConverter());", code);
        Assert.Contains("static (x, v) => x.Note = v, new global::DraftConverter());", code);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void A_converter_of_another_type_is_reported_and_the_property_is_left_out()
    {
        var result = Run("""
            using Routeables.Abstraction.States;
            public class IntConverter : IPersistConverter<int>
            {
                public string? Write(int value) => value.ToString();
                public bool TryRead(string text, out int value) => int.TryParse(text, out value);
            }
            public class Vm { [Persist(Converter = typeof(IntConverter))] public string Text { get; set; } = ""; }
            """);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("ROUT401", diagnostic.Id);
        Assert.Contains("must implement", diagnostic.GetMessage());
        Assert.Equal("", Generated(result));
    }

    [Fact]
    public void A_converter_with_no_public_constructor_without_parameter_is_reported()
    {
        var result = Run("""
            using Routeables.Abstraction.States;
            public class NeedsArg : IPersistConverter<string>
            {
                public NeedsArg(int x) { }
                public string? Write(string value) => value;
                public bool TryRead(string text, out string value) { value = text; return true; }
            }
            public class Vm { [Persist(Converter = typeof(NeedsArg))] public string Text { get; set; } = ""; }
            """);

        Assert.Contains("no parameter", Assert.Single(result.Diagnostics).GetMessage());
    }

    [Fact]
    public void A_generic_class_is_reported()
    {
        var result = Run("""
            using Routeables.Abstraction.States;
            public class Vm<T> { [Persist] public int N { get; set; } }
            """);

        Assert.Equal("ROUT401", Assert.Single(result.Diagnostics).Id);
        Assert.Equal("", Generated(result));
    }

    [Fact]
    public void Nothing_is_generated_without_the_attribute()
    {
        var result = Run("public class Vm { public int N { get; set; } }");

        Assert.Equal("", Generated(result));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void An_edit_that_changes_no_persisted_property_leaves_what_the_generator_read_unchanged()
    {
        var first = Compilation("using Routeables.Abstraction.States; public class Vm { [Persist] public int N { get; set; } }");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new PersistGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
        driver = driver.RunGenerators(first);

        var second = first.AddSyntaxTrees(CSharpSyntaxTree.ParseText("public class Other { }"));
        driver = driver.RunGenerators(second);

        var outputs = driver.GetRunResult().Results.Single().TrackedSteps["RouteablesPersist"].SelectMany(s => s.Outputs).ToList();
        Assert.NotEmpty(outputs);
        Assert.All(outputs, o => Assert.True(o.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged));
    }

    private static CSharpCompilation Compilation(string source)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Append(typeof(Routeables.Abstraction.States.PersistAttribute).Assembly)
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => a.Location).Distinct()
            .Select(location => MetadataReference.CreateFromFile(location));
        return CSharpCompilation.Create("Persist.Tests", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static GeneratorDriverRunResult Run(string source)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new PersistGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(Compilation(source), out var output, out _);
        Assert.Empty(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        return driver.GetRunResult();
    }

    private static string Generated(GeneratorDriverRunResult result) => string.Join(Environment.NewLine, result.GeneratedTrees.Select(t => t.ToString()));
}
