using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction;
using Routeables.Generated;

namespace Routeables.Tests.Generated;

public class MainViewModelLifetimeAnalyzerTests
{
    private const string Declarations = """
        using System;
        using System.Collections.ObjectModel;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection.Extensions;
        using Routeables.Abstraction;

        public class Main : IMainViewModel
        {
            public ObservableCollection<IRouteableEntry> Routeables { get; } = new();
            public IRouteableEntry? Routeable { get; set; }
            public object? CurrentView { get; set; }
            public event EventHandler<object?>? CurrentViewChanged { add { } remove { } }
            public event EventHandler? LockChanged { add { } remove { } }
        }
        """;

    private static async Task<string[]> Analyze(string statement)
    {
        var source = Declarations + $$"""

            public static class Setup
            {
                public static void Configure(IServiceCollection services)
                {
                    {{statement}}
                }
            }
            """;

        var compilation = CSharpCompilation.Create(
            "Routeables.Analyzer.Tests",
            [CSharpSyntaxTree.ParseText(source)],
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var diagnostics = await compilation
            .WithAnalyzers([new MainViewModelLifetimeAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();

        return diagnostics.Select(d => d.Id).ToArray();
    }

    [Theory]
    [InlineData("services.AddTransient<IMainViewModel, Main>();")]
    [InlineData("services.AddTransient<IMainViewModel>(sp => new Main());")]
    [InlineData("services.AddTransient(typeof(IMainViewModel), typeof(Main));")]
    [InlineData("services.TryAddTransient<IMainViewModel, Main>();")]
    public async Task A_transient_IMainViewModel_is_reported(string statement)
        => Assert.Equal(["ROUT301"], await Analyze(statement));

    [Theory]
    [InlineData("services.AddSingleton<IMainViewModel, Main>();")]
    [InlineData("services.AddScoped<IMainViewModel, Main>();")]
    [InlineData("services.RegisterMainViewModel<Main>(ServiceLifetime.Singleton);")]
    [InlineData("services.AddTransient<Main>();")]
    public async Task Other_registrations_are_not_reported(string statement)
        => Assert.Empty(await Analyze(statement));

    private static IEnumerable<MetadataReference> References()
        => AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Concat([
                MetadataReference.CreateFromFile(typeof(IMainViewModel).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location)])
            .DistinctBy(r => r.Display);
}
