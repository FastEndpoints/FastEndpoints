using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FastEndpoints.Generator;

static class Extensions
{
    extension(StringBuilder sb)
    {
        // ReSharper disable once InconsistentNaming
        internal StringBuilder w(string? val)
        {
            sb.Append(val);

            return sb;
        }

        // ReSharper disable once InconsistentNaming
        internal StringBuilder l(string? val)
        {
            sb.AppendLine(val);

            return sb;
        }
    }

    extension(string input)
    {
        //prefixes reserved keywords with '@' so that a member name can be emitted verbatim
        internal string EscapeKeyword()
            => SyntaxFacts.GetKeywordKind(input) is SyntaxKind.None ? input : $"@{input}";
    }

    internal static string ToRootNamespace(this Compilation compilation)
        => compilation.AssemblyName?.ToValidNameSpace() ?? "Assembly";

    internal static IncrementalValueProvider<string> RootNamespaceProvider(this IncrementalGeneratorInitializationContext ctx)
        => ctx.CompilationProvider.Select(static (c, _) => c.ToRootNamespace());

    internal static string RenderPreservedTypeList(string rootNamespace, string className, IEnumerable<string> typeExpressions)
    {
        //names are global:: qualified because user namespaces/types (e.g. {root}.System) can shadow unqualified ones
        var b = new StringBuilder().w(
            $$"""
              #pragma warning disable CS0618

              namespace {{rootNamespace}};

              public static class {{className}}
              {
                  public static readonly global::System.Collections.Generic.List<global::System.Type> All =
                  [
              """);

        foreach (var typeExpression in typeExpressions)
        {
            b.w(
                $"""

                         Preserve<{typeExpression}>(),
                 """);
        }

        b.w(
            """

                ];
                
                // this method instructs the native aot linker to not strip away metadata on a given type
                static global::System.Type Preserve<
                    [global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
                        global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors | 
                        global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods | 
                        global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.Interfaces)]T>() => typeof(T);
            }
            """);

        return b.ToString();
    }

    //global:: qualified names can't be shadowed by namespaces or types declared under the generated root namespace
    internal static string ToFullName(this ITypeSymbol symbol)
        => symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    //a type nested in an open generic is itself generic at runtime, and AssemblyScanner drops those (IsGenericType).
    //emitting Outer<T>.Inner writes an unbound T that does not compile.
    internal static bool IsNestedInGenericType(this SyntaxNode node)
    {
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is TypeDeclarationSyntax { TypeParameterList: not null })
                return true;
        }

        return false;
    }

    const string DontRegisterAttribute = "DontRegisterAttribute";

    extension(ITypeSymbol symbol)
    {
        internal bool HasDontRegisterAttribute()
            => symbol.GetAttributes().Any(static a => a.AttributeClass?.Name == DontRegisterAttribute);

        //walks up the base types to find FastEndpoints.Endpoint<TRequest, TResponse>, which every endpoint base class derives from
        internal INamedTypeSymbol? GetEndpointBase()
        {
            for (var type = symbol.BaseType; type is not null; type = type.BaseType)
            {
                if (type is { Name: "Endpoint", Arity: 2, ContainingNamespace: { Name: "FastEndpoints", ContainingNamespace.IsGlobalNamespace: true } })
                    return type;
            }

            return null;
        }

        internal ITypeSymbol GetUnderlyingType()
        {
            if (symbol is { IsReferenceType: true, NullableAnnotation: NullableAnnotation.Annotated })
                return symbol.WithNullableAnnotation(NullableAnnotation.NotAnnotated);

            if (symbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T && symbol is INamedTypeSymbol namedTypeSymbol)
                return namedTypeSymbol.TypeArguments[0];

            return symbol;
        }

        internal IEnumerable<ITypeSymbol> BaseTypesAndSelf()
        {
            for (var current = symbol; current is not null; current = current.BaseType)
                yield return current;
        }

        //names of the required fields and properties in the type hierarchy, most derived first
        internal IEnumerable<string> RequiredMemberNames()
            => symbol.BaseTypesAndSelf()
                     .SelectMany(static t => t.GetMembers())
                     .Where(static m => m is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true })
                     .Select(static m => m.Name)
                     .Distinct(StringComparer.Ordinal);
    }

    const string SetsRequiredMembersAttribute = "global::System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute";

    extension(IMethodSymbol method)
    {
        internal bool SetsRequiredMembers()
            => method.GetAttributes().Any(static a => a.AttributeClass?.ToFullName() == SetsRequiredMembersAttribute);
    }
}