using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace FastEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public class AccessControlGenerator : IIncrementalGenerator
{
    const string AccessControl = "AccessControl";

    // ReSharper disable once InconsistentNaming
    readonly StringBuilder b = new();

    public void Initialize(IncrementalGeneratorInitializationContext initCtx)
    {
        var assemblyName = initCtx.CompilationProvider.Select(static (c, _) => c.AssemblyName);
        initCtx.RegisterSourceOutput(
            assemblyName,
            static (spc, assemblyName) => spc.AddSource("Allow.b.g.cs", SourceText.From(RenderBase(assemblyName), Encoding.UTF8)));

        // picks up AccessControl(...) calls in endpoint declarations
        var matches = initCtx.SyntaxProvider
                             .CreateSyntaxProvider(Qualify, Transform)
                             .Where(static m => m.Endpoint is not null)
                             .WithComparer(MatchComparer.Instance)
                             .Collect();

        // picks up handwritten public static string fields in partial Allow class declarations
        var customFields = initCtx.SyntaxProvider
                                  .CreateSyntaxProvider(QualifyCustom, TransformCustom)
                                  .WithComparer(PermissionComparer.Instance)
                                  .Collect();

        // picks up partial property declarations; generator provides implementing half with computed hash code
        var partialFields = initCtx.SyntaxProvider
                                   .CreateSyntaxProvider(QualifyPartial, TransformPartial)
                                   .WithComparer(PermissionComparer.Instance)
                                   .Collect();

        initCtx.RegisterSourceOutput(matches.Combine(customFields).Combine(partialFields).Combine(assemblyName), Generate);

        //executed per each keystroke
        static bool Qualify(SyntaxNode node, CancellationToken _)
            => node is InvocationExpressionSyntax { ArgumentList.Arguments.Count: not 0, Expression: IdentifierNameSyntax { Identifier.ValueText: AccessControl } };

        //executed per each keystroke but only for syntax nodes filtered by the Qualify method
        static Match Transform(GeneratorSyntaxContext ctx, CancellationToken _)
            => new(ctx.SemanticModel.GetDeclaredSymbol(ctx.Node.Parent!.Parent!.Parent!.Parent!), (InvocationExpressionSyntax)ctx.Node);

        //executed per each keystroke
        static bool QualifyCustom(SyntaxNode node, CancellationToken _)
            => node is FieldDeclarationSyntax field &&
               field.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)) &&
               (field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)) || field.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))) &&
               field.Declaration.Type is PredefinedTypeSyntax { Keyword.ValueText: "string" } &&
               field.Parent is ClassDeclarationSyntax { Identifier.ValueText: "Allow" } cls &&
               cls.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));

        //executed per each keystroke but only for syntax nodes filtered by the Qualify method
        static Permission TransformCustom(GeneratorSyntaxContext ctx, CancellationToken _)
        {
            var field = (FieldDeclarationSyntax)ctx.Node;
            var variable = field.Declaration.Variables.FirstOrDefault();

            if (variable is null)
                return default;
            if (ctx.SemanticModel.GetDeclaredSymbol(variable) is not IFieldSymbol symbol)
                return default;
            if (!IsGeneratedAllowType(symbol.ContainingType, ctx.SemanticModel.Compilation))
                return default;

            var hideFromDocs = ctx.SemanticModel.Compilation.GetTypeByMetadataName($"{nameof(FastEndpoints)}.{nameof(HideFromDocsAttribute)}");

            if (symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, hideFromDocs)))
                return default;

            return symbol.ConstantValue is string code
                       ? new(variable.Identifier.ValueText, code)
                       : new(variable.Identifier.ValueText, variable.Identifier.ValueText, $"{variable.Identifier.Text} ?? string.Empty");
        }

        //executed per each keystroke
        static bool QualifyPartial(SyntaxNode node, CancellationToken _)
            => node is PropertyDeclarationSyntax prop &&
               prop.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)) &&
               prop.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)) &&
               prop.Modifiers.Any(m => m.ValueText == "partial") &&
               prop is { Type: PredefinedTypeSyntax { Keyword.ValueText: "string" }, AccessorList.Accessors.Count: 1 } &&
               prop.AccessorList.Accessors[0].IsKind(SyntaxKind.GetAccessorDeclaration) &&
               prop.AccessorList.Accessors[0].Body is null &&
               prop.AccessorList.Accessors[0].ExpressionBody is null &&
               prop.ExpressionBody is null &&
               prop.Parent is ClassDeclarationSyntax { Identifier.ValueText: "Allow" };

        //executed per each keystroke but only for syntax nodes filtered by the Qualify method
        static Permission TransformPartial(GeneratorSyntaxContext ctx, CancellationToken _)
        {
            var prop = (PropertyDeclarationSyntax)ctx.Node;
            var hideFromDocs = ctx.SemanticModel.Compilation.GetTypeByMetadataName($"{nameof(FastEndpoints)}.{nameof(HideFromDocsAttribute)}");

            if (ctx.SemanticModel.GetDeclaredSymbol(prop) is { } symbol &&
                IsGeneratedAllowType(symbol.ContainingType, ctx.SemanticModel.Compilation) &&
                !symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, hideFromDocs)))
                return new(prop.Identifier.ValueText);

            return default;
        }

        static bool IsGeneratedAllowType(INamedTypeSymbol? type, Compilation compilation)
        {
            var expectedNamespace = $"{compilation.AssemblyName?.ToValidNameSpace() ?? "Assembly"}.Auth";

            return type is { Name: "Allow", IsStatic: true } && type.ContainingNamespace.ToDisplayString().Equals(expectedNamespace, StringComparison.Ordinal);
        }
    }

    //only executed if the equality comparer says the data is not what has been cached by roslyn
    void Generate(SourceProductionContext spc, (((ImmutableArray<Match>, ImmutableArray<Permission>), ImmutableArray<Permission>), string?) input)
    {
        var (((matches, customPerms), partialPerms), asmName) = input;

        if (matches.Length == 0 && customPerms.Length == 0 && partialPerms.Length == 0)
            return;

        var rootNamespace = asmName?.ToValidNameSpace() ?? "Assembly";

        var endpointPerms = matches.Select(static m => new Permission(m))
                                   .OrderBy(p => p.Name)
                                   .ToArray();

        var sortedCustomPerms = customPerms.OrderBy(p => p.Name).ToArray();

        var sortedPartialPerms = partialPerms.OrderBy(p => p.Name).ToArray();

        spc.AddSource(
            "Allow.g.cs",
            SourceText.From(
                RenderClass(rootNamespace, endpointPerms, sortedCustomPerms, sortedPartialPerms),
                Encoding.UTF8));
    }

    string RenderClass(string rootNamespace, Permission[] endpointPerms, Permission[] constCustom, Permission[] partialPerms)
    {
        b.Clear().w(
            $$"""
              #nullable enable

              using FastEndpoints;
              using System;
              using System.Collections.Generic;
              using System.Linq;

              namespace {{rootNamespace}}.Auth;

              public static partial class Allow
              {

              #region ACL_ITEMS
              """);

        foreach (var p in endpointPerms)
        {
            b.w(
                $"""

                     /// <summary>{p.Description}</summary><remark>Generated from endpoint: <see cref="{p.Endpoint}"/></remark>
                     public const string {p.Name} = {CsString(p.Code)};
                 """);
        }
        b.w(
            """

            #endregion

            """);

        RenderPartialFields(b, partialPerms);

        RenderInitPermissions(b, [..endpointPerms, ..constCustom, ..partialPerms]);

        RenderGroups(b, endpointPerms);

        RenderDescriptions(b, endpointPerms);

        b.w(
            """

            }
            """);

        return b.ToString();

        static void RenderPartialFields(StringBuilder sb, Permission[] perms)
        {
            if (perms.Length == 0)
                return;

            sb.w(
                """

                #region PARTIAL_ACL_ITEMS
                """);

            foreach (var p in perms)
            {
                sb.w(
                    $$"""

                          public static partial string {{p.Name}} { get => {{CsString(p.Code)}}; }
                      """);
            }

            sb.w(
                """

                #endregion

                """);
        }

        static void RenderInitPermissions(StringBuilder sb, Permission[] perms)
        {
            sb.w(
                """

                    static partial void InitPermissions()
                    {

                """);

            foreach (var p in perms)
            {
                sb.w(
                    $"""
                             _permNames[{CsString(p.Name)}] = {p.CodeExpression};
                             _permCodes[{p.CodeExpression}] = {CsString(p.Name)};

                     """);
            }

            sb.w(
                """
                    }

                """);
        }

        static void RenderGroups(StringBuilder sb, IEnumerable<Permission> perms)
        {
            var groups = perms.SelectMany(p => p.Categories.Select(c => (category: c, name: p.Name)))
                              .OrderBy(x => x.name).ThenBy(x => x.category)
                              .GroupBy(x => x.category, x => x.name)
                              .ToDictionary(g => g.Key, g => g.Select(n => n).ToArray());

            if (groups.Count == 0)
                return;

            sb.w(
                """

                #region GROUPS
                """);

            foreach (var g in groups)
            {
                var key = $"_{g.Key.ToLower()}";
                sb.w(
                    $$"""

                          public static IEnumerable<string> {{g.Key}} => {{key}};
                          private static void AddTo{{g.Key}}(string permissionCode) => {{key}}.Add(permissionCode);
                          private static readonly List<string>{{key}} = new()
                          {

                      """);

                foreach (var name in g.Value)
                {
                    sb.w(
                        $"""
                                 {name},

                         """);
                }
                sb.Remove(sb.Length - 2, 2).w(
                    """

                        };

                    """);
            }
            sb.w(
                """
                #endregion

                """);
        }

        static void RenderDescriptions(StringBuilder sb, IEnumerable<Permission> perms)
        {
            sb.w(
                """

                #region DESCRIPTIONS
                    [HideFromDocs]
                    public static Dictionary<string, string> Descriptions = new()
                    {
                """);

            foreach (var p in perms)
            {
                if (p.Description is not null)
                {
                    sb.w(
                        $$"""

                                  //{{p.Name}}
                                  { {{CsString(p.Code)}}, {{CsString(p.Description)}} },
                          """);
                }
            }

            sb.w(
                """

                    };
                #endregion

                """);
        }
    }

    static string RenderBase(string? assemblyName)
        => $$"""
             #nullable enable

             using FastEndpoints;
             using System;
             using System.Collections.Generic;
             using System.Linq;

             namespace {{assemblyName?.ToValidNameSpace() ?? "Assembly"}}.Auth;

             public static partial class Allow
             {
                 private static readonly Dictionary<string, string> _permNames = new();
                 private static readonly Dictionary<string, string> _permCodes = new();

                 static Allow()
                 {
                     InitPermissions();
                     Groups();
                     Describe();
                 }

                 static partial void InitPermissions();

                 /// <summary>
                 /// implement this method to add custom permissions to the generated categories
                 /// </summary>
                 static partial void Groups();

                 /// <summary>
                 /// implement this method to add descriptions to your custom permissions
                 /// </summary>
                 static partial void Describe();

                 /// <summary>
                 /// gets a list of permission names for the given list of permission codes
                 /// </summary>
                 /// <param name="codes">the permission codes to get the permission names for</param>
                 public static IEnumerable<string> NamesFor(IEnumerable<string> codes)
                 {
                     foreach (var code in codes)
                         if (_permCodes.TryGetValue(code, out var name)) yield return name;
                 }

                 /// <summary>
                 /// get a list of permission codes for a given list of permission names
                 /// </summary>
                 /// <param name="names">the permission names to get the codes for</param>
                 public static IEnumerable<string> CodesFor(IEnumerable<string> names)
                 {
                     foreach (var name in names)
                         if (_permNames.TryGetValue(name, out var code)) yield return code;
                 }

                 /// <summary>
                 /// get the permission code for a given permission name
                 /// </summary>
                 /// <param name="permissionName">the name of the permission to get the code for</param>
                 public static string? PermissionCodeFor(string permissionName)
                 {
                     if (_permNames.TryGetValue(permissionName, out var code))
                         return code;
                     return null;
                 }

                 /// <summary>
                 /// get the permission name for a given permission code
                 /// </summary>
                 /// <param name="permissionCode">the permission code to get the name for</param>
                 public static string? PermissionNameFor(string permissionCode)
                 {
                     if (_permCodes.TryGetValue(permissionCode, out var name))
                         return name;
                     return null;
                 }

                 /// <summary>
                 /// get a permission tuple using it's name. returns null if not found
                 /// </summary>
                 /// <param name="permissionName">name of the permission</param>
                 public static (string PermissionName, string PermissionCode)? PermissionFromName(string permissionName)
                 {
                     if (_permNames.TryGetValue(permissionName, out var code))
                         return new(permissionName, code);
                     return null;
                 }

                 /// <summary>
                 /// get the permission tuple using it's code. returns null if not found
                 /// </summary>
                 /// <param name="permissionCode">code of the permission to get</param>
                 public static (string PermissionName, string PermissionCode)? PermissionFromCode(string permissionCode)
                 {
                     if (_permCodes.TryGetValue(permissionCode, out var name))
                         return new(name, permissionCode);
                     return null;
                 }

                 /// <summary>
                 /// get a list of all permission names
                 /// </summary>
                 public static IEnumerable<string> AllNames()
                     => _permNames.Keys;

                 /// <summary>
                 /// get a list of all permission codes
                 /// </summary>
                 public static IEnumerable<string> AllCodes()
                     => _permNames.Values;

                 /// <summary>
                 /// get a list of all the defined permissions
                 /// </summary>
                 public static IEnumerable<(string PermissionName, string PermissionCode)> AllPermissions()
                     => _permNames.Select(kv => new ValueTuple<string, string>(kv.Key, kv.Value));
             }
             """;

    static string CsString(string value)
        => SymbolDisplay.FormatLiteral(value, quote: true);

    readonly struct Permission
    {
        public string Name { get; }
        public string? Description { get; }
        public string Code { get; }
        public string CodeExpression { get; }
        public string Endpoint { get; }
        public IEnumerable<string> Categories { get; }

        internal Permission(Match m)
        {
            var args = m.Invocation
                        .ArgumentList
                        .Arguments
                        .Select(a => a.Expression)
                        .OfType<LiteralExpressionSyntax>()
                        .Select(l => l.Token.ValueText.ToValidIdentifier("_"))
                        .ToArray();

            var desc = m.Invocation.ArgumentList.OpenParenToken.TrailingTrivia.SingleOrDefault(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia))
                        .ToString();

            Name = args[0].ToValidCSharpIdentifier("_");
            Code = GetAclHash(args[0]);
            CodeExpression = CsString(Code);
            Endpoint = m.Endpoint!.ToDisplayString();
            Description = desc.Length == 0 ? null : desc.Substring(2).Trim();
            Categories = args.Skip(1).Select(static c => c.ToValidCSharpIdentifier("_")).ToArray();
        }

        internal Permission(string name, string code)
            : this(name, code, CsString(code)) { }

        internal Permission(string name, string code, string codeExpression)
        {
            Name = name;
            Code = code;
            CodeExpression = codeExpression;
            Endpoint = string.Empty;
            Description = null;
            Categories = [];
        }

        internal Permission(string name)
        {
            Name = name;
            Code = GetAclHash(name);
            CodeExpression = CsString(Code);
            Endpoint = string.Empty;
            Description = null;
            Categories = [];
        }

        static string GetAclHash(string input)
        {
            //NOTE: if modifying this algo, update FastEndpoints.Endpoint.Base.ToAclKey() method also!
            using var sha256 = SHA256.Create();
            var base64Hash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(input.ToUpperInvariant())));

            return new(base64Hash.Where(char.IsLetterOrDigit).Take(3).Select(char.ToUpper).ToArray());
        }
    }

    readonly struct Match(ISymbol? endpoint, InvocationExpressionSyntax invocation)
    {
        public ISymbol? Endpoint { get; } = endpoint;
        public InvocationExpressionSyntax Invocation { get; } = invocation;
    }

    class MatchComparer : IEqualityComparer<Match>
    {
        internal static MatchComparer Instance { get; } = new();

        MatchComparer() { }

        public bool Equals(Match x, Match y)
            => x.Endpoint!.ToDisplayString().Equals(y.Endpoint!.ToDisplayString()) &&
               x.Invocation.IsEquivalentTo(y.Invocation);

        public int GetHashCode(Match obj)
            => obj.Endpoint!.ToDisplayString().GetHashCode();
    }

    class PermissionComparer : IEqualityComparer<Permission>
    {
        internal static PermissionComparer Instance { get; } = new();

        PermissionComparer() { }

        public bool Equals(Permission x, Permission y)
            => x.Name == y.Name &&
               x.Code == y.Code &&
               x.CodeExpression == y.CodeExpression &&
               x.Endpoint == y.Endpoint &&
               x.Description == y.Description &&
               x.Categories.SequenceEqual(y.Categories);

        public int GetHashCode(Permission obj)
        {
            var hash = 17;
            Add(obj.Name);
            Add(obj.Code);
            Add(obj.CodeExpression);
            Add(obj.Endpoint);
            Add(obj.Description);

            foreach (var category in obj.Categories)
                Add(category);

            return hash;

            void Add(string? value)
                => hash = unchecked(hash * 31 + (value is null ? 0 : StringComparer.Ordinal.GetHashCode(value)));
        }
    }
}
