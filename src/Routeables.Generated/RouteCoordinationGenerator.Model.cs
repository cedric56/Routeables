using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Routeables.Generated
{
    // The records and enums the other parts share.
    public sealed partial class RouteCoordinationGenerator
    {

        private sealed class AnalysisContext
        {
            public AnalysisContext(Compilation compilation, INamedTypeSymbol routerType)
            {
                Compilation = compilation;
                RouterType = routerType;
            }

            public Compilation Compilation { get; }
            public INamedTypeSymbol RouterType { get; }
        }

        private enum ParamKind { Entry, Route, Router, Container }

        // Working data for one constructor parameter; never stored in the incremental model.
        private sealed class ParamInfo
        {
            public ParamInfo(IParameterSymbol symbol, int index, ParamKind kind, Compilation compilation)
            {
                Symbol = symbol;
                Index = index;
                Kind = kind;
                Compilation = compilation;
            }

            public IParameterSymbol Symbol { get; }
            public int Index { get; }
            public ParamKind Kind { get; private set; }
            public Compilation Compilation { get; }
            public ClrKind RouteKind { get; private set; }
            public bool RouteOptional { get; private set; }

            public string Id => EscapeIdentifier(Symbol.Name);
            public bool HasDefault => Symbol.HasExplicitDefaultValue;
            private bool IsNullableValueType => Symbol.Type is INamedTypeSymbol n && n.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

            // Null (or an unset optional value) can legitimately reach the generated method.
            public bool CanBeNull => Symbol.Type.IsReferenceType || IsNullableValueType;

            // An optional Nullable<T> keeps its type, so the value is read with .Value.
            public bool KeepsNullableValue => HasDefault && IsNullableValueType;

            // A null optional entry is simply not passed on.
            public bool NeedsConditionalEntry => HasDefault && CanBeNull;

            public void BecomeRoute(bool optional, ClrKind kind)
            {
                Kind = ParamKind.Route;
                RouteOptional = optional;
                RouteKind = kind;
            }

            public NavParam ToNavParam()
            {
                string declaration;
                string typeDisplay;
                if (HasDefault)
                {
                    declaration = Symbol.ToDisplayString(ParameterFormat);
                    typeDisplay = Symbol.Type.ToDisplayString(TypeFormat);
                }
                else
                {
                    // Without a default the value is mandatory: the container cannot be given a null.
                    var type = Symbol.Type;
                    if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                        type = named.TypeArguments[0];
                    else if (type.IsReferenceType)
                        type = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);

                    typeDisplay = type.ToDisplayString(TypeFormat);
                    declaration = typeDisplay + " " + Id;
                }

                var isString = Symbol.Type.SpecialType == SpecialType.System_String;
                var checkNotEmpty = false;
                var checkNotNull = false;
                if (Kind == ParamKind.Route)
                {
                    if (!RouteOptional)
                    {
                        checkNotEmpty = isString;
                        checkNotNull = !isString && KeepsNullableValue;
                    }
                }
                else
                {
                    checkNotNull = !HasDefault && Symbol.Type.IsReferenceType;
                }

                return new NavParam(Id, declaration, typeDisplay, checkNotNull, checkNotEmpty);
            }
        }

        private static string EscapeIdentifier(string name) =>
            SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

        private sealed record AnalysisResult(EquatableArray<NavMethod> Methods, EquatableArray<DiagInfo> Diagnostics)
        {
            public static readonly AnalysisResult Empty = new AnalysisResult(
                new EquatableArray<NavMethod>(Array.Empty<NavMethod>()),
                new EquatableArray<DiagInfo>(Array.Empty<DiagInfo>()));
        }

        private sealed record DiagInfo(string Id, string Message, LocationInfo? Location);

        private sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
        {
            public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

            public static LocationInfo? From(Location? location)
            {
                if (location is null || location.SourceTree is null)
                    return null;

                return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
            }
        }

        private sealed record NavMethod(
            string CallerName,
            string CallerNamespace,
            string CallerHint,
            string MethodName,
            string DestinationName,
            string DestinationDisplay,
            string Template,
            bool LeadingSlash,
            EquatableArray<NavParam> Parameters,
            EquatableArray<EntryOp> Entries,
            EquatableArray<RouteOp> RouteOps,
            LocationInfo? Location);

        private sealed record NavParam(string Id, string Declaration, string TypeDisplay, bool CheckNotNull, bool CheckNotEmpty);

        private sealed record EntryOp(bool IsRouter, string ParamId, bool Conditional);

        private enum RouteOpKind { PathLiteral, PathParam, QueryLiteral, QueryParam }

        private enum ClrKind { None, String, Int, Long, Bool, Guid, Decimal, Double, Rest }

        // Text: the literal, or the query key. Prefix: the separator written before a path segment.
        private sealed record RouteOp(
            RouteOpKind Kind,
            string Prefix,
            string Text,
            string ParamId,
            ClrKind Clr,
            bool Optional,
            bool Conditional,
            bool NullableValue);

        private sealed class TemplatePart
        {
            public TemplatePart(bool isQuery, bool isPlaceholder, string text, string name, string type, bool isViewBound, bool optional, bool isRest = false)
            {
                IsQuery = isQuery;
                IsPlaceholder = isPlaceholder;
                Text = text;
                Name = name;
                Type = type;
                IsViewBound = isViewBound;
                Optional = optional;
                IsRest = isRest;
            }

            public bool IsQuery { get; }
            public bool IsPlaceholder { get; }
            public string Text { get; }
            public string Name { get; }
            public string Type { get; }
            public bool IsViewBound { get; }
            public bool Optional { get; }
            public bool IsRest { get; }

            public static TemplatePart Literal(bool isQuery, string text) =>
                new TemplatePart(isQuery, isPlaceholder: false, text, string.Empty, string.Empty, isViewBound: false, optional: false);
        }
    }
}
