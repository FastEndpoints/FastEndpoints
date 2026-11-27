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

        //quotes and escapes a user supplied value so that it can be emitted as a C# string literal
        internal string ToCSharpLiteral()
            => SymbolDisplay.FormatLiteral(input, quote: true);
    }

    internal static string ToRootNamespace(this Compilation compilation)
        => compilation.AssemblyName?.ToValidNameSpace() ?? "Assembly";

    internal static IncrementalValueProvider<string> RootNamespaceProvider(this IncrementalGeneratorInitializationContext ctx)
        => ctx.CompilationProvider.Select(static (c, _) => c.ToRootNamespace());

    //generated code references user types as they are. a bare [Obsolete] raises CS0612 and [Obsolete("...")] raises CS0618, neither of which users can suppress there.
    internal const string DisableObsoleteWarnings = "#pragma warning disable CS0612, CS0618";

    internal static string RenderPreservedTypeList(string rootNamespace, string className, IEnumerable<string> typeExpressions)
    {
        //names are global:: qualified because user namespaces/types (e.g. {root}.System) can shadow unqualified ones
        var b = new StringBuilder().w(
            $$"""
              {{DisableObsoleteWarnings}}

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

    //a class or record declaration that AssemblyScanner could register: it can implement interfaces only if at least one of its declarations
    //has a base list. each generator adds its own rule for open generic declarations.
    internal static bool IsCandidateTypeDeclaration(this SyntaxNode node)
        => node is ClassDeclarationSyntax or RecordDeclarationSyntax &&
           node is TypeDeclarationSyntax { BaseList: not null } &&
           !node.IsNestedInGenericType();

    // ReSharper disable InconsistentNaming
    //metadata names (see MetadataFullName) of the FastEndpoints contracts that more than one generator matches
    internal const string IEndpoint = "FastEndpoints.IEndpoint";
    internal const string IPreProcessor = "FastEndpoints.IPreProcessor`1";
    internal const string IPostProcessor = "FastEndpoints.IPostProcessor`2";
    internal const string EndpointBase = "FastEndpoints.Endpoint`2";

    // ReSharper restore InconsistentNaming

    const string DontRegisterAttribute = "FastEndpoints.DontRegisterAttribute";

    extension(ISymbol symbol)
    {
        internal bool HasAttribute(string metadataFullName)
            => symbol.GetAttributes().Any(a => a.Matches(metadataFullName));
    }

    extension(AttributeData attribute)
    {
        //like the runtime's exact type checks, a same named attribute from another namespace doesn't match
        internal bool Matches(string metadataFullName)
            => attribute.AttributeClass?.MetadataFullName() == metadataFullName;
    }

    extension(ITypeSymbol symbol)
    {
        //namespace qualified metadata name with the generic arity, e.g. "FastEndpoints.IPreProcessor`1" for any IPreProcessor<T>.
        //unlike display string prefixes, it tells arities apart and doesn't match lookalikes such as IEndpointFactory for IEndpoint.
        internal string MetadataFullName()
            => symbol.ContainingType is { } parent
                   ? $"{parent.MetadataFullName()}+{symbol.MetadataName}"
                   : symbol.ContainingNamespace is { IsGlobalNamespace: false } ns
                       ? $"{ns.ToDisplayString()}.{symbol.MetadataName}"
                       : symbol.MetadataName;

        internal bool ImplementsInterface(string metadataFullName)
            => symbol.AllInterfaces.Any(i => i.MetadataFullName() == metadataFullName);

        //mirrors the abstract and [DontRegister] checks of AssemblyScanner.IsTypeMatch(). each generator handles generic types itself.
        internal bool IsRegistrable()
            => !symbol.IsAbstract && !symbol.HasAttribute(DontRegisterAttribute);

        //walks up the base types to find FastEndpoints.Endpoint<TRequest, TResponse>, which every endpoint base class derives from
        internal INamedTypeSymbol? GetEndpointBase()
        {
            for (var type = symbol.BaseType; type is not null; type = type.BaseType)
            {
                if (type.MetadataFullName() == EndpointBase)
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

    const string SetsRequiredMembersAttribute = "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute";

    extension(IMethodSymbol method)
    {
        //names of the required members a constructor leaves unset, which an object initializer after it must assign
        internal IEnumerable<string> UnsetRequiredMembers()
            => method.HasAttribute(SetsRequiredMembersAttribute) ? [] : method.ContainingType.RequiredMemberNames();
    }
}