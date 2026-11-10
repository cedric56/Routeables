using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;

namespace Routeables.Generated;

/// <summary>
/// ROUT301: <c>IRouteableMenuViewModel</c> registered as transient. Each consumer (the window, the lock coordinator, the views)
/// would get another instance than the one that is displayed, so the selection, the locks and the subscriptions would
/// land on an object nobody sees. <c>RegisterMenuViewModel</c> refuses it at run time; this reports the direct
/// <c>AddTransient</c> at build time.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MenuViewModelLifetimeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ROUT301";

    private static readonly DiagnosticDescriptor TransientMain = new DiagnosticDescriptor(
        DiagnosticId,
        "IRouteableMenuViewModel registered as transient",
        "IRouteableMenuViewModel must be Scoped (Blazor Server) or Singleton (Avalonia, MAUI): a transient one gives each consumer another instance than the one that is displayed. Use RegisterMenuViewModel<T>().",
        "Routeables",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(TransientMain);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var main = start.Compilation.GetTypeByMetadataName("Routeables.Abstraction.Bases.IRouteableMenuViewModel");
            if (main is null)
                return;

            start.RegisterOperationAction(ctx => Analyze(ctx, main), OperationKind.Invocation);
        });
    }

    private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol main)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (method.Name != "AddTransient" && method.Name != "TryAddTransient")
            return;

        // AddTransient<IRouteableMenuViewModel, MainViewModel>() / AddTransient<IRouteableMenuViewModel>(factory)
        var generic = method.TypeArguments.Length > 0 && SymbolEqualityComparer.Default.Equals(method.TypeArguments[0], main);

        // AddTransient(typeof(IRouteableMenuViewModel), typeof(MainViewModel))
        var typeOf = false;
        foreach (var argument in invocation.Arguments)
        {
            var value = argument.Value;
            while (value is IConversionOperation conversion)
                value = conversion.Operand;

            if (value is ITypeOfOperation t && SymbolEqualityComparer.Default.Equals(t.TypeOperand, main))
                typeOf = true;
        }

        if (generic || typeOf)
            context.ReportDiagnostic(Diagnostic.Create(TransientMain, invocation.Syntax.GetLocation()));
    }
}
