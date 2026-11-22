using Microsoft.CodeAnalysis;

namespace Routeables.Generated
{
    // Reading a route template: its path and query pieces and the type each one asks for.
    public sealed partial class RouteCoordinationGenerator
    {

        private static bool TryParseTemplate(string template, out List<TemplatePart> parts, out string error)
        {
            parts = new List<TemplatePart>();
            error = string.Empty;

            SplitTemplate(template, out var path, out var query);

            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < segments.Length; i++)
            {
                if (!TryReadPart(segments[i], isQuery: false, out var part, out error))
                    return false;
                parts.Add(part);
            }

            var queryParts = query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < queryParts.Length; i++)
            {
                if (!TryReadPart(queryParts[i], isQuery: true, out var part, out error))
                    return false;
                parts.Add(part);
            }

            // An omitted optional path segment can only be a trailing one.
            var lastPath = -1;
            for (var i = 0; i < parts.Count; i++)
            {
                if (!parts[i].IsQuery)
                    lastPath = i;
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (!part.IsPlaceholder)
                    continue;

                if (part.IsViewBound)
                {
                    error = "the view-bound placeholder {" + part.Name + "::" + part.Type + "} is not supported by generated route methods.";
                    return false;
                }

                if (part.IsRest)
                {
                    if (part.IsQuery)
                    {
                        error = "the catch-all {**" + part.Name + "} can only be in the path, not in the query.";
                        return false;
                    }

                    if (i != lastPath)
                    {
                        error = "the catch-all {**" + part.Name + "} must be the last path segment.";
                        return false;
                    }

                    if (part.Type.Length > 0 && !string.Equals(part.Type, "string", StringComparison.OrdinalIgnoreCase))
                    {
                        error = "the catch-all {**" + part.Name + ":" + part.Type + "} must be a string.";
                        return false;
                    }
                }

                if (!names.Add(part.Name))
                {
                    error = "the placeholder {" + part.Name + "} is used twice.";
                    return false;
                }

                if (part.Optional && !part.IsQuery && i != lastPath)
                {
                    error = "the optional placeholder {" + part.Name + "?} must be the last path segment.";
                    return false;
                }

                if (ClrKindOf(part.Type) == ClrKind.None)
                {
                    error = "the placeholder {" + part.Name + ":" + part.Type + "} has a type that generated methods cannot format (supported: string, int, long, bool, guid, decimal, double).";
                    return false;
                }
            }

            return true;
        }

        private static bool TryReadPart(string text, bool isQuery, out TemplatePart part, out string error)
        {
            error = string.Empty;
            var match = Placeholder.Match(text);
            if (match.Success)
            {
                part = new TemplatePart(
                    isQuery,
                    isPlaceholder: true,
                    text,
                    match.Groups["name"].Value,
                    match.Groups["type"].Value,
                    match.Groups["attribute"].Success,
                    match.Groups["opt"].Success,
                    match.Groups["rest"].Success);
                return true;
            }

            if (text.IndexOf('{') >= 0 || text.IndexOf('}') >= 0)
            {
                part = TemplatePart.Literal(isQuery, text);
                error = "'" + text + "' is not a valid placeholder (expected {name}, {name:type} or {name:type?}).";
                return false;
            }

            part = TemplatePart.Literal(isQuery, text);
            return true;
        }

        // The rule of RouteParser.Split, shared in AddressRules: a '?' inside braces does not start the query.
        private static void SplitTemplate(string template, out string path, out string query)
        {
            var at = Routeables.Shared.AddressRules.TemplateQueryStart(template);
            path = at < 0 ? template : template.Substring(0, at);
            query = at < 0 ? string.Empty : template.Substring(at + 1);
        }

        private static ClrKind ClrKindOf(string type)
        {
            switch (type.ToLowerInvariant())
            {
                case "":
                case "string":
                    return ClrKind.String;
                case "int":
                    return ClrKind.Int;
                case "long":
                    return ClrKind.Long;
                case "bool":
                    return ClrKind.Bool;
                case "guid":
                    return ClrKind.Guid;
                case "decimal":
                    return ClrKind.Decimal;
                case "double":
                    return ClrKind.Double;
                default:
                    return ClrKind.None;
            }
        }

        private static ClrKind ClrKindOf(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                type = named.TypeArguments[0];

            switch (type.SpecialType)
            {
                case SpecialType.System_String: return ClrKind.String;
                case SpecialType.System_Int32: return ClrKind.Int;
                case SpecialType.System_Int64: return ClrKind.Long;
                case SpecialType.System_Boolean: return ClrKind.Bool;
                case SpecialType.System_Decimal: return ClrKind.Decimal;
                case SpecialType.System_Double: return ClrKind.Double;
            }

            return type.ToDisplayString() == "System.Guid" ? ClrKind.Guid : ClrKind.None;
        }
    }
}
