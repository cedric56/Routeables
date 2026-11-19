using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text;

namespace Routeables.Generated
{
    // Choosing the constructor and telling where each parameter comes from: the route, the container or the caller.
    public sealed partial class RouteCoordinationGenerator
    {

        private static bool TrySelectConstructor(
            AnalysisContext context,
            INamedTypeSymbol destination,
            out IMethodSymbol? constructor,
            out string? error)
        {
            constructor = null;
            error = null;

            var candidates = new List<IMethodSymbol>();
            foreach (var c in destination.InstanceConstructors)
            {
                if (c.DeclaredAccessibility == Accessibility.Public)
                    candidates.Add(c);
            }

            if (candidates.Count == 0)
            {
                error = "'" + destination.Name + "' has no public constructor.";
                return false;
            }

            var marked = new List<IMethodSymbol>();
            foreach (var c in candidates)
            {
                foreach (var a in c.GetAttributes())
                {
                    if (a.AttributeClass?.ToDisplayString() == ActivatorCtorMetadataName)
                    {
                        marked.Add(c);
                        break;
                    }
                }
            }

            if (marked.Count > 1)
            {
                error = "'" + destination.Name + "' has several constructors marked [ActivatorUtilitiesConstructor].";
                return false;
            }

            if (marked.Count == 1)
            {
                constructor = marked[0];
                return true;
            }

            var max = 0;
            foreach (var c in candidates)
                max = Math.Max(max, c.Parameters.Length);

            var longest = candidates.Where(c => c.Parameters.Length == max).ToList();
            if (longest.Count > 1)
            {
                error = "'" + destination.Name + "' has several public constructors with " + max + " parameters: mark one with [ActivatorUtilitiesConstructor].";
                return false;
            }

            constructor = longest[0];
            return true;
        }

        private static List<ParamInfo>? ClassifyParameters(
            AnalysisContext context,
            IMethodSymbol constructor,
            List<TemplatePart> parts,
            string template,
            Action<string, string> report)
        {
            var compilation = context.Compilation;
            var result = new List<ParamInfo>();
            var failed = false;

            for (var i = 0; i < constructor.Parameters.Length; i++)
            {
                var p = constructor.Parameters[i];

                if (p.RefKind != RefKind.None || p.Type.TypeKind == TypeKind.Pointer ||
                    p.Type.TypeKind == TypeKind.FunctionPointer || p.Type.IsRefLikeType)
                {
                    report("ROUT105", "Constructor parameter '" + p.Name + "' (" + p.Type.ToDisplayString() + ") cannot be passed through a route method: ref, out, in, pointer and ref struct parameters are not supported.");
                    failed = true;
                    continue;
                }

                var kind = ParamKind.Entry;
                if (SymbolEqualityComparer.Default.Equals(p.Type, context.RouterType))
                    kind = ParamKind.Router;
                else if (IsResolvedFromContainer(p))
                    kind = ParamKind.Container;

                result.Add(new ParamInfo(p, i, kind, compilation));
            }

            if (failed)
                return null;

            // Route placeholders map to constructor parameters (by name, as the route parser fills them).
            foreach (var part in parts)
            {
                if (!part.IsPlaceholder)
                    continue;

                ParamInfo? target = null;
                foreach (var info in result)
                {
                    if (info.Kind == ParamKind.Entry && string.Equals(info.Symbol.Name, part.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        target = info;
                        break;
                    }
                }

                if (target is null)
                {
                    report("ROUT104", "Route '" + template + "': the placeholder {" + part.Name + "} has no matching constructor parameter. " +
                        "Parameters of type IRouter or marked [FromContainer] cannot receive route values.");
                    failed = true;
                    continue;
                }

                var expected = ClrKindOf(part.Type);
                var actual = ClrKindOf(target.Symbol.Type);
                if (actual != expected)
                {
                    report("ROUT104", "Route '" + template + "': the placeholder {" + part.Name + ":" + part.Type + "} does not match the type " +
                        target.Symbol.Type.ToDisplayString() + " of constructor parameter '" + target.Symbol.Name + "'.");
                    failed = true;
                    continue;
                }

                if (part.Optional && !target.HasDefault)
                {
                    report("ROUT104", "Route '" + template + "': the placeholder {" + part.Name + "?} is optional, so constructor parameter '" +
                        target.Symbol.Name + "' needs a default value.");
                    failed = true;
                    continue;
                }

                // A catch-all may be empty for the router ("/files" matches "/files/{**path}"), so with a default value its parameter is optional.
                target.BecomeRoute(part.Optional || (part.IsRest && target.HasDefault), part.IsRest ? ClrKind.Rest : expected);
            }

            if (failed)
                return null;

            foreach (var info in result)
            {
                if (info.Kind != ParamKind.Entry && info.Kind != ParamKind.Route)
                    continue;

                foreach (var reserved in ReservedNames)
                {
                    if (string.Equals(info.Symbol.Name, reserved, StringComparison.Ordinal))
                    {
                        report("ROUT105", "Constructor parameter '" + info.Symbol.Name + "' clashes with a name used by the generated method: rename it.");
                        failed = true;
                    }
                }
            }

            return failed ? null : result;
        }

        private static bool IsResolvedFromContainer(IParameterSymbol parameter)
        {
            foreach (var a in parameter.GetAttributes())
            {
                var name = a.AttributeClass?.ToDisplayString();
                if (name == FromContainerMetadataName || name == FromKeyedServicesMetadataName || name == FromServicesMetadataName)
                    return true;
            }

            return false;
        }

        // ActivatorUtilities matches the given arguments to constructor parameters by type, in order. A parameter that may not receive an entry
        // of its own (optional, from the container, or filled from the route afterwards) can swallow the value meant for another parameter of a
        // compatible type.
        private static void WarnAboutAmbiguousMatching(
            AnalysisContext context,
            INamedTypeSymbol destination,
            List<ParamInfo> parameters,
            Action<string, string> report)
        {
            foreach (var gap in parameters)
            {
                var mayBeUnfilled = gap.Kind == ParamKind.Container || gap.Kind == ParamKind.Route ||
                                    (gap.Kind == ParamKind.Entry && gap.HasDefault);
                if (!mayBeUnfilled)
                    continue;

                foreach (var other in parameters)
                {
                    var supplies = other.Kind == ParamKind.Router || other.Kind == ParamKind.Entry;
                    if (!supplies || ReferenceEquals(other, gap))
                        continue;

                    // Entries before the gap are already matched to their own parameters.
                    if (gap.Kind != ParamKind.Route && other.Index < gap.Index)
                        continue;

                    if (Assignable(context.Compilation, other.Symbol.Type, gap.Symbol.Type) ||
                        Assignable(context.Compilation, gap.Symbol.Type, other.Symbol.Type))
                    {
                        report("ROUT106", "'" + destination.Name + "': constructor parameter '" + gap.Symbol.Name + "' (" + gap.Symbol.Type.ToDisplayString() +
                            ") may receive the value meant for '" + other.Symbol.Name + "' (" + other.Symbol.Type.ToDisplayString() +
                            ") because the container matches constructor arguments by type. Use distinct types.");
                        break;
                    }
                }
            }
        }

        private static bool Assignable(Compilation compilation, ITypeSymbol from, ITypeSymbol to)
        {
            var c = compilation.ClassifyConversion(from, to);
            return c.Exists && c.IsImplicit && (c.IsIdentity || c.IsReference || c.IsBoxing || c.IsNullable);
        }


        private static List<RouteOp> BuildRouteOps(List<TemplatePart> parts, List<ParamInfo> parameters)
        {
            var ops = new List<RouteOp>();
            var firstPath = true;

            foreach (var part in parts)
            {
                // Path segments are joined with '/'; the template's own leading slash is written separately.
                var prefix = string.Empty;
                if (!part.IsQuery)
                {
                    prefix = firstPath ? string.Empty : "/";
                    firstPath = false;
                }

                if (!part.IsPlaceholder)
                {
                    ops.Add(new RouteOp(
                        part.IsQuery ? RouteOpKind.QueryLiteral : RouteOpKind.PathLiteral,
                        prefix, part.Text, string.Empty, ClrKind.None,
                        Optional: false, Conditional: false, NullableValue: false));
                    continue;
                }

                ParamInfo? info = null;
                foreach (var candidate in parameters)
                {
                    if (candidate.Kind == ParamKind.Route && string.Equals(candidate.Symbol.Name, part.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        info = candidate;
                        break;
                    }
                }

                ops.Add(new RouteOp(
                    part.IsQuery ? RouteOpKind.QueryParam : RouteOpKind.PathParam,
                    prefix,
                    part.IsQuery ? part.Name : string.Empty,
                    info!.Id,
                    info.RouteKind,
                    Optional: info.RouteOptional,
                    Conditional: info.RouteOptional && info.CanBeNull,
                    NullableValue: info.KeepsNullableValue));
            }

            return ops;
        }

        private static string DefaultMethodName(INamedTypeSymbol destination)
        {
            var name = destination.Name;
            const string suffix = "ViewModel";
            if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
                name = name.Substring(0, name.Length - suffix.Length);

            return "GoTo" + name + "Async";
        }

        private static string CallerHint(INamedTypeSymbol caller)
        {
            var sb = new StringBuilder();
            var chain = new List<string>();
            for (var t = caller; t is not null; t = t.ContainingType)
                chain.Insert(0, t.Name);

            if (!caller.ContainingNamespace.IsGlobalNamespace)
                sb.Append(caller.ContainingNamespace.ToDisplayString().Replace('.', '_')).Append('_');

            sb.Append(string.Join("_", chain));
            return sb.ToString();
        }
    }
}
