using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace Routeables.Generated
{
    /// <summary>
    /// Writes, for the properties marked <c>[Persist]</c>, the code that saves their value with the pages of the history and gives it back after the
    /// process was killed: a registration in <c>RouteablePersistence</c>, made when the assembly starts. No reflection: the code reads and writes the
    /// properties directly.
    /// </summary>
    [Generator]
    public sealed class PersistGenerator : IIncrementalGenerator
    {
        private const string TrackingName = "RouteablesPersist";
        private const string ConverterMetadataName = "Routeables.Abstraction.States.IPersistConverter`1";
        private const string PersistAttributeMetadataName = "Routeables.Abstraction.States.PersistAttribute";

        private static readonly DiagnosticDescriptor Unsupported = new DiagnosticDescriptor(
            "ROUT401", "A [Persist] property cannot be saved", "{0}", "Routeables.Persistence",
            DiagnosticSeverity.Warning, isEnabledByDefault: true);

        private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        // What the generator keeps of a property: values only, so that the pipeline sees an unchanged model after an edit that changes none of them.
        private sealed record PersistProperty(
            string ContainerType, string Name, string PropertyType, string? Converter, string? Problem,
            string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var properties = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    PersistAttributeMetadataName,
                    predicate: static (node, _) => node is PropertyDeclarationSyntax,
                    transform: static (ctx, _) => Read(ctx))
                .Where(static p => p is not null)
                .Select(static (p, _) => p!)
                .Collect()
                .Select(static (all, _) => new EquatableArray<PersistProperty>(all.ToArray()))
                .WithTrackingName(TrackingName);

            context.RegisterSourceOutput(properties.Combine(context.CompilationProvider.Select(static (c, _) => c.AssemblyName ?? "assembly")),
                static (spc, input) => Emit(spc, input.Left, input.Right));
        }

        private static PersistProperty? Read(GeneratorAttributeSyntaxContext ctx)
        {
            if (!(ctx.TargetSymbol is IPropertySymbol property))
                return null;

            var container = property.ContainingType;
            var location = ctx.TargetNode.GetLocation();
            string? problem = null;

            if (property.IsStatic)
                problem = "a static property is not part of a view model's state";
            else if (property.IsIndexer)
                problem = "an indexer cannot be saved";
            else if (property.GetMethod is null || property.SetMethod is null)
                problem = "it needs a getter and a setter";
            else if (property.SetMethod.IsInitOnly)
                problem = "an init-only property cannot be set once the page is built";
            else if (!IsReachable(property.GetMethod.DeclaredAccessibility) || !IsReachable(property.SetMethod.DeclaredAccessibility)
                     || !IsReachable(property.DeclaredAccessibility))
                problem = "its getter and setter must be public or internal";
            else if (container.IsGenericType)
                problem = "the class is generic";
            else if (!IsTypeReachable(container))
                problem = "the class (or a class around it) is private or protected";
            else if (property.Type is ITypeParameterSymbol || property.Type.TypeKind == TypeKind.Pointer || property.Type.TypeKind == TypeKind.FunctionPointer)
                problem = "its type cannot be saved";
            else if (property.Type is INamedTypeSymbol named && named.IsRefLikeType)
                problem = "a ref struct cannot be saved";

            string? converter = null;
            foreach (var attribute in ctx.Attributes)
            {
                foreach (var argument in attribute.NamedArguments)
                {
                    if (argument.Key != "Converter" || argument.Value.Value is not INamedTypeSymbol converterType)
                        continue;

                    var wanted = ctx.SemanticModel.Compilation.GetTypeByMetadataName(ConverterMetadataName)?.Construct(property.Type);
                    if (wanted is null || !converterType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, wanted)))
                        problem ??= $"{converterType.Name} must implement IPersistConverter<{property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}>";
                    else if (converterType.IsAbstract || converterType.IsGenericType
                             || !converterType.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public)
                             || !IsTypeReachable(converterType))
                        problem ??= $"{converterType.Name} must be a public, non-generic, non-abstract class with a public constructor that has no parameter";
                    else
                        converter = converterType.ToDisplayString(TypeFormat);
                }
            }

            return new PersistProperty(
                container.ToDisplayString(TypeFormat),
                property.Name,
                property.Type.ToDisplayString(TypeFormat),
                converter,
                problem,
                location.SourceTree?.FilePath ?? "",
                location.SourceSpan,
                location.GetLineSpan().Span);
        }

        private static bool IsReachable(Accessibility a)
            => a == Accessibility.Public || a == Accessibility.Internal || a == Accessibility.ProtectedOrInternal;

        private static bool IsTypeReachable(INamedTypeSymbol type)
        {
            for (var t = type; t != null; t = t.ContainingType)
            {
                if (!IsReachable(t.DeclaredAccessibility) && !(t.ContainingType is null && t.DeclaredAccessibility == Accessibility.NotApplicable))
                    return false;
            }

            return true;
        }

        private static void Emit(SourceProductionContext spc, EquatableArray<PersistProperty> input, string assemblyName)
        {
            var usable = new List<PersistProperty>();
            foreach (var property in input)
            {
                if (property.Problem is null)
                {
                    usable.Add(property);
                    continue;
                }

                spc.ReportDiagnostic(Diagnostic.Create(
                    Unsupported,
                    Location.Create(property.FilePath, property.TextSpan, property.LineSpan),
                    $"[Persist] on '{property.ContainerType}.{property.Name}' is ignored: {property.Problem}."));
            }

            if (usable.Count == 0)
                return;

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/> Routeables.Generated.PersistGenerator");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("#pragma warning disable CS8620, CS8604");
            sb.AppendLine();
            sb.AppendLine("namespace Routeables.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    internal static class RouteablePersistRegistration");
            sb.AppendLine("    {");
            sb.AppendLine("#pragma warning disable CA2255");
            sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
            sb.AppendLine("#pragma warning restore CA2255");
            sb.AppendLine("        internal static void Register()");
            sb.AppendLine("        {");

            foreach (var group in usable.GroupBy(p => p.ContainerType).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var type = group.Key;
                var ordered = group.OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
                sb.AppendLine($"            global::Routeables.Abstraction.States.RouteablePersistence.Register<{type}>(");
                sb.AppendLine("                static (m, bag) =>");
                sb.AppendLine("                {");
                foreach (var p in ordered)
                    sb.AppendLine($"                    bag.Capture<{type}, {p.PropertyType}>(m, \"{p.Name}\", static x => x.{p.Name}{Converter(p)});");
                sb.AppendLine("                },");
                sb.AppendLine("                static (m, bag) =>");
                sb.AppendLine("                {");
                foreach (var p in ordered)
                    sb.AppendLine($"                    bag.Restore<{type}, {p.PropertyType}>(m, \"{p.Name}\", static (x, v) => x.{p.Name} = v{Converter(p)});");
                sb.AppendLine("                });");
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            spc.AddSource("Routeables.Persistence." + Sanitize(assemblyName) + ".g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static string Converter(PersistProperty p) => p.Converter is null ? "" : $", new {p.Converter}()";

        private static string Sanitize(string name)
        {
            var chars = name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
            return new string(chars);
        }
    }
}
