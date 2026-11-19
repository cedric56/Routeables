using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace Routeables.Generated
{
    /// <summary>
    /// Generates strongly typed routing methods for view models decorated with
    /// <c>[RouteableViewModel(typeof(Caller))]</c>: one extension method per declared caller type,
    /// whose parameters mirror the destination's constructor.
    /// </summary>
    [Generator]
    public sealed partial class RouteCoordinationGenerator : IIncrementalGenerator
    {
        private const string ViewModelAttributeMetadataName = "Routeables.Abstraction.RouteableViewModelAttribute";
        private const string RouteableAttributeMetadataName = "Routeables.Abstraction.RouteableAttribute";
        private const string RouterMetadataName = "Routeables.Abstraction.Routing.IRouter";
        private const string FromContainerMetadataName = "Routeables.Abstraction.FromContainerAttribute";
        private const string FromKeyedServicesMetadataName = "Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute";
        private const string FromServicesMetadataName = "Microsoft.AspNetCore.Mvc.FromServicesAttribute";
        private const string ActivatorCtorMetadataName = "Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructorAttribute";

        private const string Category = "Routeables.Routing";

        private static readonly Dictionary<string, DiagnosticDescriptor> Descriptors = new Dictionary<string, DiagnosticDescriptor>(StringComparer.Ordinal)
        {
            ["ROUT101"] = Descriptor("ROUT101", "Invalid route caller", DiagnosticSeverity.Error),
            ["ROUT102"] = Descriptor("ROUT102", "Invalid route destination", DiagnosticSeverity.Error),
            ["ROUT103"] = Descriptor("ROUT103", "Route not found", DiagnosticSeverity.Error),
            ["ROUT104"] = Descriptor("ROUT104", "Invalid route template", DiagnosticSeverity.Error),
            ["ROUT105"] = Descriptor("ROUT105", "Unsupported constructor parameter", DiagnosticSeverity.Error),
            ["ROUT106"] = Descriptor("ROUT106", "Ambiguous constructor argument matching", DiagnosticSeverity.Warning),
            ["ROUT107"] = Descriptor("ROUT107", "Duplicate route method", DiagnosticSeverity.Error),
        };

        private static DiagnosticDescriptor Descriptor(string id, string title, DiagnosticSeverity severity) =>
            new DiagnosticDescriptor(id, title, "{0}", Category, severity, isEnabledByDefault: true,
                helpLinkUri: "https://cedric56.github.io/Routeables/guides/diagnostics.html#" + id.ToLowerInvariant());

        // Mirrors RouteParser.Param: {name}, {name:type}, {name:type?}, {name::type} (view-bound), {**name} (the rest of the path).
        private static readonly Regex Placeholder =
            new Regex(@"^\{(?<rest>\*\*)?(?<name>\w+)(?::(?<attribute>:)?(?<type>[^}?]*))?(?<opt>\?)?\}$", RegexOptions.CultureInvariant);

        private static readonly string[] ReservedNames = { "caller", "router", "cancellationToken", "__args", "__route", "__separator" };

        // Type of the generated method parameter, with the default value when there is one.
        private static readonly SymbolDisplayFormat ParameterFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions:
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier,
            parameterOptions:
                SymbolDisplayParameterOptions.IncludeType |
                SymbolDisplayParameterOptions.IncludeName |
                SymbolDisplayParameterOptions.IncludeDefaultValue);

        private static readonly SymbolDisplayFormat TypeFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions:
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var results = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    ViewModelAttributeMetadataName,
                    predicate: static (node, _) => node is TypeDeclarationSyntax,
                    transform: static (ctx, ct) => Analyze(ctx, ct))
                .Collect();

            context.RegisterSourceOutput(results, static (spc, all) => Emit(spc, all));
        }

    }
}