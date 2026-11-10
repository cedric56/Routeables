using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Routeables.Abstraction.Bases;
using Routeables.Generated;

namespace Routeables.Tests.Generated;

public class MenuViewModelLifetimeAnalyzerTests
{
    private const string Declarations = """
        using System;
        using System.Collections.ObjectModel;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.DependencyInjection.Extensions;
        using Routeables.Abstraction;
        using Routeables.Abstraction.Bases;
        public class Main : IRouteableMenuViewModel
        {
            public ObservableCollection<IRouteableMenuEntry> Entries { get; } = new();
            public IRouteableMenuEntry? SelectedEntry { get; set; }
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
            .WithAnalyzers([new MenuViewModelLifetimeAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();

        return diagnostics.Select(d => d.Id).ToArray();
    }

    [Theory]
    [InlineData("services.AddTransient<IRouteableMenuViewModel, Main>();")]
    [InlineData("services.AddTransient<IRouteableMenuViewModel>(sp => new Main());")]
    [InlineData("services.AddTransient(typeof(IRouteableMenuViewModel), typeof(Main));")]
    [InlineData("services.TryAddTransient<IRouteableMenuViewModel, Main>();")]
    public async Task A_transient_IRouteableMenuViewModel_is_reported(string statement)
        => Assert.Equal(["ROUT301"], await Analyze(statement));

    [Theory]
    [InlineData("services.AddSingleton<IRouteableMenuViewModel, Main>();")]
    [InlineData("services.AddScoped<IRouteableMenuViewModel, Main>();")]
    [InlineData("services.RegisterMenuViewModel<Main>(ServiceLifetime.Singleton);")]
    [InlineData("services.AddTransient<Main>();")]
    public async Task Other_registrations_are_not_reported(string statement)
        => Assert.Empty(await Analyze(statement));

    private static IEnumerable<MetadataReference> References()
        => AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Concat([
                MetadataReference.CreateFromFile(typeof(IRouteableMenuViewModel).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location)])
            .DistinctBy(r => r.Display);
}
