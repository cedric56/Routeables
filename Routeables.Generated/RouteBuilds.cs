using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text;

namespace Routeables.Generated
{
    /// <summary>
    /// How the types of the registered routes are built and filled without reflection: the constructor to call and the properties
    /// the injection may write. The registration generator emits the code from this; a type it cannot describe completely is left to
    /// the reflection of the library.
    /// </summary>
    internal static class RouteBuilds
    {
        private const string ActivatorCtorMetadataName = "Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructorAttribute";
        private const string RouteablesParametersName = "Routeables.Abstraction.RouteablesParameters";

        private static readonly SymbolDisplayFormat Fq = SymbolDisplayFormat.FullyQualifiedFormat;

        private static readonly SymbolDisplayFormat FqNullable = SymbolDisplayFormat.FullyQualifiedFormat
            .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        // ------------------------------------------------------------------ model

        internal sealed record ParamInfo(string Name, string Type, bool HasDefault, string? Default);

        internal sealed record PropInfo(string Name, string Type, string RawType, bool Bag, bool CanGet, EquatableArray<string> Attributes);

        internal sealed record BuildInfo(
            string TypeName,
            bool CanConstruct,
            EquatableArray<ParamInfo> Parameters,
            bool TakesRouter,
            bool CanDescribe,
            EquatableArray<string> TypeAttributes,
            EquatableArray<PropInfo> Properties);

        // ------------------------------------------------------------------ analysis

        public static BuildInfo Analyze(Compilation compilation, INamedTypeSymbol type)
        {
            var typeName = type.ToDisplayString(Fq);
            var usable = type.TypeKind == TypeKind.Class
                && !type.IsStatic
                && !HasGenericScope(type)
                && compilation.IsSymbolAccessibleWithin(type, compilation.Assembly);

            var parameters = new List<ParamInfo>();
            var takesRouter = false;
            var canConstruct = usable && !type.IsAbstract && !HasRequiredMembers(type)
                && TryReadConstructor(compilation, type, parameters, out takesRouter);
            if (!canConstruct)
            {
                parameters.Clear();
                takesRouter = false;
            }

            var typeAttributes = new List<string>();
            var properties = new List<PropInfo>();
            var canDescribe = usable && TryReadInjection(compilation, type, typeAttributes, properties);
            if (!canDescribe)
            {
                typeAttributes.Clear();
                properties.Clear();
            }

            return new BuildInfo(
                typeName,
                canConstruct,
                new EquatableArray<ParamInfo>(parameters.ToArray()),
                takesRouter,
                canDescribe,
                new EquatableArray<string>(typeAttributes.ToArray()),
                new EquatableArray<PropInfo>(properties.ToArray()));
        }

        private static bool HasGenericScope(INamedTypeSymbol type)
        {
            for (var t = type; t is not null; t = t.ContainingType)
                if (t.IsGenericType)
                    return true;
            return false;
        }

        private static bool HasRequiredMembers(INamedTypeSymbol type)
        {
            for (var t = type; t is not null; t = t.BaseType)
            {
                foreach (var member in t.GetMembers())
                {
                    if (member is IPropertySymbol { IsRequired: true } || member is IFieldSymbol { IsRequired: true })
                        return true;
                }
            }
            return false;
        }

        // The constructor ActivatorUtilities would call: the only public one, or the one marked [ActivatorUtilitiesConstructor].
        // With several the choice is made at run time from what can be resolved: that is left to ActivatorUtilities.
        private static bool TryReadConstructor(Compilation compilation, INamedTypeSymbol type, List<ParamInfo> parameters, out bool takesRouter)
        {
            takesRouter = false;

            var publicConstructors = type.InstanceConstructors
                .Where(c => c.DeclaredAccessibility == Accessibility.Public)
                .ToList();
            var marked = publicConstructors
                .Where(c => c.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == ActivatorCtorMetadataName))
                .ToList();

            IMethodSymbol constructor;
            if (marked.Count == 1)
                constructor = marked[0];
            else if (marked.Count == 0 && publicConstructors.Count == 1)
                constructor = publicConstructors[0];
            else
                return false;

            foreach (var p in constructor.Parameters)
            {
                if (p.RefKind != RefKind.None || p.IsParams || p.GetAttributes().Length > 0)
                    return false;
                if (!IsUsableType(compilation, p.Type))
                    return false;

                string? literal = null;
                if (p.HasExplicitDefaultValue && !TryFormatDefault(p.Type, p.ExplicitDefaultValue, out literal))
                    return false;

                if (p.Type.ToDisplayString(Fq) == "global::Routeables.Abstraction.IRouter")
                    takesRouter = true;

                parameters.Add(new ParamInfo(p.Name, p.Type.ToDisplayString(FqNullable), p.HasExplicitDefaultValue, literal));
            }

            return true;
        }

        private static bool IsUsableType(Compilation compilation, ITypeSymbol type)
        {
            if (type.IsRefLikeType || type.TypeKind == TypeKind.Pointer || type.TypeKind == TypeKind.FunctionPointer
                || type.TypeKind == TypeKind.Error || type.TypeKind == TypeKind.Dynamic)
                return false;
            return compilation.IsSymbolAccessibleWithin(type, compilation.Assembly);
        }

        private static bool TryFormatDefault(ITypeSymbol type, object? value, out string? literal)
        {
            literal = null;
            if (value is null)
            {
                literal = "default!";
                return true;
            }

            var enumType = (type as INamedTypeSymbol)?.EnumUnderlyingType is not null ? type : null;
            if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
                && nullable.TypeArguments[0].TypeKind == TypeKind.Enum)
                enumType = nullable.TypeArguments[0];

            if (enumType is not null)
            {
                literal = "(" + enumType.ToDisplayString(Fq) + ")" + SymbolDisplay.FormatPrimitive(value, quoteStrings: true, useHexadecimalNumbers: false);
                return true;
            }

            if (value is string or bool or char or sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal)
            {
                literal = SymbolDisplay.FormatPrimitive(value, quoteStrings: true, useHexadecimalNumbers: false);
                return literal is not null;
            }

            return false;
        }

        // The type and its base types, up to but not including object.
        private static IEnumerable<INamedTypeSymbol> WithBaseTypes(INamedTypeSymbol type)
        {
            for (var t = type; t is not null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
                yield return t;
        }

        // The properties the injection could write, and the attributes of the type. It is complete when every property that could be
        // written (it has an attribute, or the type names properties through its own attributes) can be written by code outside the type.
        private static bool TryReadInjection(Compilation compilation, INamedTypeSymbol type, List<string> typeAttributes, List<PropInfo> properties)
        {
            // The compiler hands the generator only the public and protected members of the types of other assemblies. An internal
            // property there (an [Inject] internal on a base component, say) is invisible to the generator and found by reflection:
            // such a type cannot be described completely, so it is left to reflection.
            if (WithBaseTypes(type).Any(t => !SymbolEqualityComparer.Default.Equals(t.ContainingAssembly, compilation.Assembly)))
                return false;

            // The attributes of the type, those it inherits included, as expressions that build them.
            var typeAttributeData = WithBaseTypes(type)
                .SelectMany(t => t.GetAttributes().Where(attribute =>
                    attribute.AttributeClass is { } attributeClass
                    && !IsNoise(attributeClass, typeLevel: true)
                    && (ReferenceEquals(t, type) || IsInherited(attributeClass))))
                .ToList();

            foreach (var attribute in typeAttributeData)
            {
                var expression = AttributeExpression(compilation, attribute);
                if (expression is null)
                    return false;
                typeAttributes.Add(expression);
            }

            var typeNamesProperties = typeAttributeData.Count > 0;
            var seen = new Dictionary<string, IPropertySymbol>(StringComparer.Ordinal);

            foreach (var t in WithBaseTypes(type))
            {
                foreach (var property in t.GetMembers().OfType<IPropertySymbol>())
                {
                    if (property.IsStatic || property.IsIndexer)
                        continue;
                    // Reflection does not return the private properties of a base type.
                    if (!ReferenceEquals(t, type) && property.DeclaredAccessibility == Accessibility.Private)
                        continue;

                    if (seen.TryGetValue(property.Name, out var derived))
                    {
                        // The override of a property is the one found first, in the most derived type; a property that hides
                        // another with the same name would be two for reflection.
                        if (!Overrides(derived, property))
                            return false;
                        continue;
                    }
                    seen[property.Name] = property;

                    // No setter: reflection does not return it as writable either.
                    if (property.SetMethod is null)
                        continue;

                    if (!TryReadProperty(compilation, property, typeNamesProperties, out var info))
                        return false;
                    if (info is not null)
                        properties.Add(info);
                }
            }

            return true;
        }

        // False when the injection could write the property but the generated code could not: the description would be incomplete.
        // <paramref name="info"/> stays null for a property the injection never writes.
        private static bool TryReadProperty(Compilation compilation, IPropertySymbol property, bool typeNamesProperties, out PropInfo? info)
        {
            info = null;

            var attributes = AttributeNames(property);
            var bag = property.Type.ToDisplayString(Fq) == "global::" + RouteablesParametersName;
            if (attributes.Count == 0 && !bag && !typeNamesProperties)
                return true;

            var writable = property.SetMethod is { IsInitOnly: false }
                && compilation.IsSymbolAccessibleWithin(property.SetMethod!, compilation.Assembly)
                && IsUsableType(compilation, property.Type);
            if (!writable)
                return false;

            var canGet = property.GetMethod is not null && compilation.IsSymbolAccessibleWithin(property.GetMethod, compilation.Assembly);
            if (property.GetMethod is not null && !canGet)
                return false;

            info = new PropInfo(
                property.Name,
                property.Type.ToDisplayString(FqNullable),
                property.Type.ToDisplayString(Fq),
                bag,
                canGet,
                new EquatableArray<string>(attributes.OrderBy(a => a, StringComparer.Ordinal).ToArray()));
            return true;
        }

        private static bool Overrides(IPropertySymbol derived, IPropertySymbol baseProperty)
        {
            for (var p = derived.OverriddenProperty; p is not null; p = p.OverriddenProperty)
                if (SymbolEqualityComparer.Default.Equals(p.OriginalDefinition, baseProperty.OriginalDefinition))
                    return true;
            return false;
        }

        // The full names (as Type.FullName gives them) of the attributes declared on a property and of their base types.
        // PropertyInfo.IsDefined ignores "inherit", so an override does not see the attributes of the property it overrides.
        private static HashSet<string> AttributeNames(IPropertySymbol property)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var attribute in property.GetAttributes())
            {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass is null || IsNoise(attributeClass, typeLevel: false))
                    continue;

                foreach (var c in WithBaseTypes(attributeClass))
                {
                    var name = ClrFullName(c);
                    if (name != "System.Attribute")
                        names.Add(name);
                }
            }
            return names;
        }

        // Attributes the injection has no use for: the ones of the compiler and the base library, and the ones that declare the routes.
        private static bool IsNoise(INamedTypeSymbol attribute, bool typeLevel)
        {
            var ns = attribute.ContainingNamespace is { IsGlobalNamespace: false } containing ? containing.ToDisplayString() : "";
            if (ns == "System" || ns.StartsWith("System.Runtime.CompilerServices", StringComparison.Ordinal)
                || ns.StartsWith("System.Diagnostics", StringComparison.Ordinal)
                || ns.StartsWith("System.ComponentModel.DataAnnotations", StringComparison.Ordinal)
                || ns.StartsWith("System.Text.Json", StringComparison.Ordinal)
                || ns.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal))
                return true;

            return typeLevel && ns == "Routeables.Abstraction"
                && (attribute.Name == "RouteableAttribute" || attribute.Name == "RouteableViewAttribute" || attribute.Name == "RouteableViewModelAttribute");
        }

        private static bool IsInherited(INamedTypeSymbol attribute)
        {
            for (var c = attribute; c is not null; c = c.BaseType)
            {
                foreach (var usage in c.GetAttributes())
                {
                    if (usage.AttributeClass?.ToDisplayString() != "System.AttributeUsageAttribute")
                        continue;
                    foreach (var named in usage.NamedArguments)
                        if (named.Key == "Inherited" && named.Value.Value is bool inherited)
                            return inherited;
                    return true;
                }
            }
            return true;
        }

        private static string ClrFullName(INamedTypeSymbol type)
        {
            var name = type.MetadataName;
            if (type.ContainingType is not null)
                return ClrFullName(type.ContainingType) + "+" + name;

            var ns = type.ContainingNamespace;
            return ns is null || ns.IsGlobalNamespace ? name : ns.ToDisplayString() + "." + name;
        }

        private static string? AttributeExpression(Compilation compilation, AttributeData attribute)
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass is null || attribute.AttributeConstructor is null
                || !compilation.IsSymbolAccessibleWithin(attributeClass, compilation.Assembly)
                || !compilation.IsSymbolAccessibleWithin(attribute.AttributeConstructor, compilation.Assembly))
                return null;

            var sb = new StringBuilder("new ").Append(attributeClass.ToDisplayString(Fq)).Append('(');
            var first = true;
            foreach (var argument in attribute.ConstructorArguments)
            {
                if (argument.Kind == TypedConstantKind.Error)
                    return null;
                if (!first) sb.Append(", ");
                first = false;
                sb.Append(argument.ToCSharpString());
            }
            sb.Append(')');

            if (attribute.NamedArguments.Length > 0)
            {
                sb.Append(" { ");
                var firstNamed = true;
                foreach (var named in attribute.NamedArguments)
                {
                    if (named.Value.Kind == TypedConstantKind.Error)
                        return null;
                    if (!firstNamed) sb.Append(", ");
                    firstNamed = false;
                    sb.Append(named.Key).Append(" = ").Append(named.Value.ToCSharpString());
                }
                sb.Append(" }");
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------------ emission

        /// <summary>The method that builds a view model or a view: the constructor is called, then the injection finishes it.</summary>
        public static void EmitFactory(StringBuilder sb, string methodName, BuildInfo info, bool viewModel)
        {
            var arguments = new StringBuilder();
            for (var i = 0; i < info.Parameters.Count; i++)
            {
                var p = info.Parameters[i];
                if (i > 0) arguments.Append(", ");
                arguments.Append("c.Next<").Append(p.Type).Append(">(").Append(Quote(p.Name));
                if (p.HasDefault)
                    arguments.Append(", ").Append(p.Default);
                arguments.Append(')');
            }

            sb.AppendLine($"    private static {info.TypeName} {methodName}(global::System.IServiceProvider sp, global::Routeables.Abstraction.IRouteableArgs args)");
            sb.AppendLine("    {");
            var shortName = Quote(info.TypeName.StartsWith("global::", StringComparison.Ordinal) ? info.TypeName.Substring(8) : info.TypeName);
            if (viewModel)
            {
                if (info.Parameters.Count > 0)
                {
                    var names = string.Join(", ", Enumerable.Range(0, info.Parameters.Count).Select(i => Quote(info.Parameters[i].Name)));
                    sb.AppendLine($"        var c = global::Routeables.Abstraction.ConstructorArguments.ForViewModel(sp, args, {shortName}, new string[] {{ {names} }}, {(info.TakesRouter ? "true" : "false")});");
                }
                sb.AppendLine($"        return global::Routeables.Abstraction.RouteableBuild.InjectViewModel(new {info.TypeName}({arguments}), sp, args);");
            }
            else
            {
                if (info.Parameters.Count > 0)
                    sb.AppendLine($"        var c = global::Routeables.Abstraction.ConstructorArguments.ForView(sp, args, {shortName});");
                sb.AppendLine($"        return global::Routeables.Abstraction.RouteableBuild.InjectView(new {info.TypeName}({arguments}), sp, args);");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        /// <summary>The method that describes how the injection fills a type.</summary>
        public static void EmitDescription(StringBuilder sb, string methodName, BuildInfo info)
        {
            sb.AppendLine($"    private static global::Routeables.Abstraction.InjectableType {methodName}() => new(");
            if (info.TypeAttributes.Count == 0)
                sb.AppendLine("        global::System.Array.Empty<global::System.Attribute>(),");
            else
                sb.AppendLine("        new global::System.Attribute[] { " + string.Join(", ", info.TypeAttributes) + " },");

            sb.AppendLine("        new global::Routeables.Abstraction.InjectableProperty[]");
            sb.AppendLine("        {");
            foreach (var p in info.Properties)
            {
                var names = p.Attributes.Count == 0
                    ? "global::System.Array.Empty<string>()"
                    : "new string[] { " + string.Join(", ", p.Attributes.Select(Quote)) + " }";
                var get = p.CanGet ? $"o => (({info.TypeName})o).{p.Name}" : "null";
                sb.AppendLine($"            new({Quote(p.Name)}, typeof({p.RawType}), {names},");
                sb.AppendLine($"                (o, v) => (({info.TypeName})o).{p.Name} = v is null ? default! : ({p.Type})v,");
                sb.AppendLine($"                {get}),");
            }
            sb.AppendLine("        });");
            sb.AppendLine();
        }

        private static string Quote(string value)
            => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}