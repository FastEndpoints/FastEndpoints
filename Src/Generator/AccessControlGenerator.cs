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

        var matches = initCtx.SyntaxProvider
                             .CreateSyntaxProvider(Qualify, Transform)
                             .Where(static m => m.Endpoint is not null)
                             .WithComparer(MatchComparer.Instance)
                             .Collect();

        // picks up hand-written public const string fields in partial Allow class declarations
        var customFields = initCtx.SyntaxProvider
                                  .CreateSyntaxProvider(QualifyCustom, TransformCustom)
                                  .Where(static p => p.Name is not null)
                                  .Collect();

        // picks up partial field declarations — generator provides implementing half with computed hash code
        var partialFields = initCtx.SyntaxProvider
                                   .CreateSyntaxProvider(QualifyPartial, TransformPartial)
                                   .Where(static p => p.Name is not null)
                                   .Collect();

        initCtx.RegisterSourceOutput(matches.Combine(customFields).Combine(partialFields).Combine(assemblyName), Generate);

        //executed per each keystroke
        static bool Qualify(SyntaxNode node, CancellationToken _)
            => node is InvocationExpressionSyntax { ArgumentList.Arguments.Count: not 0, Expression: IdentifierNameSyntax { Identifier.ValueText: AccessControl } };

        //executed per each keystroke but only for syntax nodes filtered by the Qualify method
        static Match Transform(GeneratorSyntaxContext ctx, CancellationToken _)
            => new(ctx.SemanticModel.GetDeclaredSymbol(ctx.Node.Parent!.Parent!.Parent!.Parent!), (InvocationExpressionSyntax)ctx.Node);

        static bool QualifyCustom(SyntaxNode node, CancellationToken _)
            => node is FieldDeclarationSyntax field
               && field.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))
               && field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword))
               && field.Declaration.Type is PredefinedTypeSyntax { Keyword.ValueText: "string" }
               && field.Parent is ClassDeclarationSyntax { Identifier.ValueText: "Allow" };

        static Permission TransformCustom(GeneratorSyntaxContext ctx, CancellationToken _)
        {
            var field = (FieldDeclarationSyntax)ctx.Node;
            var variable = field.Declaration.Variables.FirstOrDefault();
            if (variable is null) return default;
            if (ctx.SemanticModel.GetDeclaredSymbol(variable) is not IFieldSymbol symbol)
                return default;
            var hideFromDocs = ctx.SemanticModel.Compilation.GetTypeByMetadataName($"{nameof(FastEndpoints)}.{nameof(HideFromDocsAttribute)}");
            if (symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, hideFromDocs)))
                return default;
            if (symbol.ConstantValue is not string code)
                return default;
            return new(variable.Identifier.ValueText, code);
        }

        static bool QualifyPartial(SyntaxNode node, CancellationToken _)
            => node is PropertyDeclarationSyntax prop
               && prop.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))
               && prop.Modifiers.Any(m => m.ValueText == "partial")
               && prop.Type is PredefinedTypeSyntax { Keyword.ValueText: "string" }
               && prop.AccessorList?.Accessors.Count == 1
               && prop.AccessorList.Accessors[0].IsKind(SyntaxKind.GetAccessorDeclaration)
               && prop.AccessorList.Accessors[0].Body is null
               && prop.AccessorList.Accessors[0].ExpressionBody is null
               && prop.ExpressionBody is null
               && prop.Parent is ClassDeclarationSyntax { Identifier.ValueText: "Allow" };

        static Permission TransformPartial(GeneratorSyntaxContext ctx, CancellationToken _)
        {
            var prop = (PropertyDeclarationSyntax)ctx.Node;
            var hideFromDocs = ctx.SemanticModel.Compilation.GetTypeByMetadataName($"{nameof(FastEndpoints)}.{nameof(HideFromDocsAttribute)}");
            if (ctx.SemanticModel.GetDeclaredSymbol(prop) is IPropertySymbol symbol
                && symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, hideFromDocs)))
                return default;
            return new(prop.Identifier.ValueText);
        }
    }

    //only executed if the equality comparer says the data is not what has been cached by roslyn
    void Generate(SourceProductionContext spc, (((ImmutableArray<Match>, ImmutableArray<Permission>), ImmutableArray<Permission>), string?) input)
    {
        var (((matches, constCustom), partialPerms), asmName) = input;

        if (matches.Length == 0 && constCustom.Length == 0 && partialPerms.Length == 0)
            return;

        var rootNamespace = asmName?.ToValidNameSpace() ?? "Assembly";

        var endpointPerms = matches.Select(static m => new Permission(m))
                                   .OrderBy(p => p.Name)
                                   .ToArray();

        var constCustomPerms = constCustom.OrderBy(p => p.Name).ToArray();

        var sortedPartialPerms = partialPerms.OrderBy(p => p.Name).ToArray();

        spc.AddSource("Allow.g.cs", SourceText.From(
            RenderClass(rootNamespace, endpointPerms, constCustomPerms, sortedPartialPerms),
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
            if (perms.Length == 0) return;

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
                             _permNames[{CsString(p.Name)}] = {CsString(p.Code)};
                             _permCodes[{CsString(p.Code)}] = {CsString(p.Name)};

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
        public string Endpoint { get; }
        public IEnumerable<string> Categories { get; }

        internal Permission(Match m)
        {
            var args = m.Invocation
                        .ArgumentList
                        .Arguments
                        .Select(a => a.Expression)
                        .OfType<LiteralExpressionSyntax>()
                        .Select(l => l.Token.ValueText.ToValidIdentifier("_"));

            var desc = m.Invocation.ArgumentList.OpenParenToken.TrailingTrivia.SingleOrDefault(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia))
                        .ToString();

            Name = args.First();
            Code = GetAclHash(Name);
            Endpoint = m.Endpoint!.ToDisplayString();
            Description = desc.Length == 0 ? null : desc.Substring(2).Trim();
            Categories = args.Skip(1);
        }

        internal Permission(string name, string code)
        {
            Name = name;
            Code = code;
            Endpoint = string.Empty;
            Categories = [];
        }

        internal Permission(string name)
        {
            Name = name;
            Code = GetAclHash(name);
            Endpoint = string.Empty;
            Description = null;
            Categories = [];
        }

        static readonly SHA256 _sha256 = SHA256.Create();

        static string GetAclHash(string input)
        {
            //NOTE: if modifying this algo, update FastEndpoints.Endpoint.Base.ToAclKey() method also!
            var base64Hash = Convert.ToBase64String(_sha256.ComputeHash(Encoding.UTF8.GetBytes(input.ToUpperInvariant())));

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
}
