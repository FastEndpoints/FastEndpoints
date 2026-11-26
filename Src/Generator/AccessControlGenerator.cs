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

    static readonly DiagnosticDescriptor _duplicatePermissionCode = new(
        id: "FEAC001",
        title: "Duplicate permission code",
        messageFormat: "Permission code '{0}' is assigned to multiple permissions: {1}. Rename one of the permissions so each permission code is unique.",
        category: "FastEndpoints.AccessControl",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext initCtx)
    {
        var rootNamespace = initCtx.RootNamespaceProvider();
        initCtx.RegisterSourceOutput(
            rootNamespace,
            static (spc, rootNamespace) => spc.AddSource("Allow.b.g.cs", SourceText.From(RenderBase(rootNamespace), Encoding.UTF8)));

        // picks up AccessControl(...) calls in endpoint declarations
        var matches = initCtx.SyntaxProvider
                             .CreateSyntaxProvider(Qualify, Transform)
                             .Where(static p => p.IsValid)
                             .Collect();

        // picks up handwritten public static string fields in partial Allow class declarations
        var customFields = initCtx.SyntaxProvider
                                  .CreateSyntaxProvider(QualifyCustom, TransformCustom)
                                  .Where(static p => p.IsValid)
                                  .Collect();

        // picks up partial property declarations; generator provides implementing half with computed hash code
        var partialFields = initCtx.SyntaxProvider
                                   .CreateSyntaxProvider(QualifyPartial, TransformPartial)
                                   .Where(static p => p.IsValid)
                                   .Collect();

        initCtx.RegisterSourceOutput(matches.Combine(customFields).Combine(partialFields).Combine(rootNamespace), Generate);

        //executed per each keystroke
        static bool Qualify(SyntaxNode node, CancellationToken _)
            => node is InvocationExpressionSyntax { ArgumentList.Arguments.Count: not 0, Expression: IdentifierNameSyntax { Identifier.ValueText: AccessControl } };

        //executed per each keystroke but only for syntax nodes filtered by the Qualify method
        static Permission Transform(GeneratorSyntaxContext ctx, CancellationToken _)
        {
            var invocation = (InvocationExpressionSyntax)ctx.Node;

            //the call can be nested in blocks, lambdas, local functions etc. of the endpoint class
            if (invocation.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } typeDeclaration ||
                ctx.SemanticModel.GetDeclaredSymbol(typeDeclaration) is not { } endpoint)
                return default;

            var arguments = invocation.ArgumentList.Arguments;
            var permissionArg = arguments.FirstOrDefault(static a => a.NameColon?.Name.Identifier.ValueText == "keyName") ?? arguments[0];

            if (TryGetStringConstant(ctx.SemanticModel, permissionArg.Expression) is not { } permissionName)
                return default;

            var groupNames = arguments.Where(a => a != permissionArg)
                                      .SelectMany(a => GetStringConstants(ctx.SemanticModel, a.Expression))
                                      .ToArray();
            var description = invocation.ArgumentList.OpenParenToken.TrailingTrivia
                                        .SingleOrDefault(static t => t.IsKind(SyntaxKind.SingleLineCommentTrivia))
                                        .ToString();

            return new(endpoint.ToFullName(), permissionName, groupNames, description.Length == 0 ? null : description.Substring(2).Trim());
        }

        // resolves string literals, const fields, nameof(...), and other compile-time string constants
        static string? TryGetStringConstant(SemanticModel model, ExpressionSyntax expression)
        {
            if (expression is LiteralExpressionSyntax { Token.Value: string literal })
                return literal;

            var constant = model.GetConstantValue(expression);

            return constant is { HasValue: true, Value: string value }
                       ? value
                       : null;
        }

        static IEnumerable<string> GetStringConstants(SemanticModel model, ExpressionSyntax expression)
        {
            if (TryGetStringConstant(model, expression) is { } value)
            {
                yield return value;

                yield break;
            }

            var elements = expression switch
            {
                ArrayCreationExpressionSyntax { Initializer: { } initializer } => initializer.Expressions,
                ImplicitArrayCreationExpressionSyntax { Initializer: { } initializer } => initializer.Expressions,
                CollectionExpressionSyntax collection => collection.Elements.OfType<ExpressionElementSyntax>().Select(static e => e.Expression),
                _ => []
            };

            foreach (var element in elements)
            {
                foreach (var constant in GetStringConstants(model, element))
                    yield return constant;
            }
        }

        //executed per each keystroke
        //selects each declarator so that 'const string A = "1", B = "2";' yields both fields.
        //type and static-ness are checked semantically, since any partial part may omit 'static' and the type may be spelled 'String'.
        static bool QualifyCustom(SyntaxNode node, CancellationToken _)
            => node is VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax field } &&
               field.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)) &&
               (field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)) || field.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))) &&
               field.Parent is ClassDeclarationSyntax { Identifier.ValueText: "Allow" };

        //executed per each keystroke but only for syntax nodes filtered by the Qualify method
        static Permission TransformCustom(GeneratorSyntaxContext ctx, CancellationToken _)
        {
            var variable = (VariableDeclaratorSyntax)ctx.Node;

            if (ctx.SemanticModel.GetDeclaredSymbol(variable) is not IFieldSymbol { Type.SpecialType: SpecialType.System_String } symbol)
                return default;
            if (!IsGeneratedAllowType(symbol.ContainingType, ctx.SemanticModel.Compilation))
                return default;

            if (IsHiddenFromDocs(symbol, ctx.SemanticModel.Compilation))
                return default;

            return symbol.ConstantValue is string code
                       ? new(variable.Identifier.ValueText, code)
                       : new(variable.Identifier.ValueText, variable.Identifier.ValueText, $"{variable.Identifier.Text} ?? string.Empty", isCodeKnown: false);
        }

        //executed per each keystroke
        static bool QualifyPartial(SyntaxNode node, CancellationToken _)
            => node is PropertyDeclarationSyntax prop &&
               prop.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)) &&
               prop.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)) &&
               prop.Modifiers.Any(m => m.ValueText == "partial") &&
               prop is { AccessorList.Accessors.Count: 1 } &&
               prop.AccessorList.Accessors[0].IsKind(SyntaxKind.GetAccessorDeclaration) &&
               prop.AccessorList.Accessors[0].Body is null &&
               prop.AccessorList.Accessors[0].ExpressionBody is null &&
               prop.ExpressionBody is null &&
               prop.Parent is ClassDeclarationSyntax { Identifier.ValueText: "Allow" };

        //executed per each keystroke but only for syntax nodes filtered by the Qualify method
        static Permission TransformPartial(GeneratorSyntaxContext ctx, CancellationToken _)
        {
            var prop = (PropertyDeclarationSyntax)ctx.Node;

            if (ctx.SemanticModel.GetDeclaredSymbol(prop) is { Type.SpecialType: SpecialType.System_String } symbol &&
                IsGeneratedAllowType(symbol.ContainingType, ctx.SemanticModel.Compilation) &&
                !IsHiddenFromDocs(symbol, ctx.SemanticModel.Compilation))
                return new(prop.Identifier.ValueText);

            return default;
        }

        static bool IsHiddenFromDocs(ISymbol symbol, Compilation compilation)
        {
            var hideFromDocs = compilation.GetTypeByMetadataName($"{nameof(FastEndpoints)}.{nameof(HideFromDocsAttribute)}");

            return symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, hideFromDocs));
        }

        static bool IsGeneratedAllowType(INamedTypeSymbol? type, Compilation compilation)
        {
            var expectedNamespace = $"{compilation.ToRootNamespace()}.Auth";

            return type is { Name: "Allow", IsStatic: true } && type.ContainingNamespace.ToDisplayString().Equals(expectedNamespace, StringComparison.Ordinal);
        }
    }

    //only executed if the equality comparer says the data is not what has been cached by roslyn
    static void Generate(SourceProductionContext spc, (((ImmutableArray<Permission>, ImmutableArray<Permission>), ImmutableArray<Permission>), string) input)
    {
        var (((matches, customPerms), partialPerms), rootNamespace) = input;

        if (matches.Length == 0 && customPerms.Length == 0 && partialPerms.Length == 0)
            return;

        var endpointPerms = matches.OrderBy(static p => p.Name, StringComparer.Ordinal).ToArray();

        var sortedCustomPerms = customPerms.OrderBy(static p => p.Name, StringComparer.Ordinal).ToArray();

        var sortedPartialPerms = partialPerms.OrderBy(static p => p.Name, StringComparer.Ordinal).ToArray();

        ReportDuplicatePermissionCodes(spc, [.. endpointPerms, .. sortedCustomPerms, .. sortedPartialPerms]);

        spc.AddSource(
            "Allow.g.cs",
            SourceText.From(
                RenderClass(rootNamespace, endpointPerms, sortedCustomPerms, sortedPartialPerms),
                Encoding.UTF8));
    }

    static void ReportDuplicatePermissionCodes(SourceProductionContext spc, Permission[] perms)
    {
        var duplicates = perms.Where(static p => p.IsCodeKnown)
                              .GroupBy(static p => p.Code)
                              .Select(
                                  static g => new
                                  {
                                      Code = g.Key,
                                      Names = g.Select(static p => p.Name).Distinct().OrderBy(static n => n, StringComparer.Ordinal).ToArray()
                                  })
                              .Where(static g => g.Names.Length > 1);

        foreach (var duplicate in duplicates)
            spc.ReportDiagnostic(Diagnostic.Create(_duplicatePermissionCode, Location.None, duplicate.Code, string.Join(", ", duplicate.Names)));
    }

    static string RenderClass(string rootNamespace, Permission[] endpointPerms, Permission[] constCustom, Permission[] partialPerms)
    {
        var b = new StringBuilder().w(
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

                     /// <summary>{XmlEscape(p.Description)}</summary><remark>Generated from endpoint: <see cref="{p.Endpoint}"/></remark>
                     public const string {p.Name} = {CsString(p.Code)};
                 """);
        }
        b.w(
            """

            #endregion

            """);

        RenderPartialFields(b, partialPerms);

        RenderInitPermissions(b, [.. endpointPerms, .. constCustom, .. partialPerms]);

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

                    static partial void InitPermissions(Dictionary<string, string> permNames, Dictionary<string, string> permCodes)
                    {

                """);

            foreach (var p in perms)
            {
                sb.w(
                    $"""
                             permNames[{CsString(p.Name)}] = {p.CodeExpression};
                             permCodes[{p.CodeExpression}] = {CsString(p.Name)};

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
                              .OrderBy(static x => x.name, StringComparer.Ordinal).ThenBy(static x => x.category, StringComparer.Ordinal)
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
                var key = $"_{g.Key.ToLowerInvariant()}";
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

    static string RenderBase(string rootNamespace)
        => $$"""
             #nullable enable

             using FastEndpoints;
             using System;
             using System.Collections.Frozen;
             using System.Collections.Generic;
             using System.Linq;

             namespace {{rootNamespace}}.Auth;

             public static partial class Allow
             {
                 private static readonly FrozenDictionary<string, string> _permNames;
                 private static readonly FrozenDictionary<string, string> _permCodes;

                 static Allow()
                 {
                     var permNames = new Dictionary<string, string>();
                     var permCodes = new Dictionary<string, string>();
                     InitPermissions(permNames, permCodes);
                     _permNames = permNames.ToFrozenDictionary();
                     _permCodes = permCodes.ToFrozenDictionary();
                     Groups();
                     Describe();
                 }

                 static partial void InitPermissions(Dictionary<string, string> permNames, Dictionary<string, string> permCodes);

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

    static string? XmlEscape(string? value)
        => value?.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    readonly record struct Permission
    {
        public string Name { get; }
        public string? Description { get; }
        public string Code { get; }
        public string CodeExpression { get; }
        public string Endpoint { get; }
        public EquatableArray<string> Categories { get; }
        public bool IsCodeKnown { get; }
        public bool IsValid => Name is not null;

        internal Permission(string endpoint, string permissionName, string[] groupNames, string? description)
        {
            permissionName = permissionName.ToValidIdentifier("_");

            Name = permissionName.ToValidCSharpIdentifier("_");
            Code = GetAclHash(permissionName);
            CodeExpression = CsString(Code);
            Endpoint = endpoint;
            Description = description;
            Categories = new([.. groupNames.Select(static c => c.ToValidIdentifier("_").ToValidCSharpIdentifier("_"))]);
            IsCodeKnown = true;
        }

        internal Permission(string name, string code)
            : this(name, code, CsString(code)) { }

        internal Permission(string name, string code, string codeExpression, bool isCodeKnown = true)
        {
            Name = name;
            Code = code;
            CodeExpression = codeExpression;
            Endpoint = string.Empty;
            Description = null;
            Categories = default;
            IsCodeKnown = isCodeKnown;
        }

        internal Permission(string name)
        {
            Name = name;
            Code = GetAclHash(name);
            CodeExpression = CsString(Code);
            Endpoint = string.Empty;
            Description = null;
            Categories = default;
            IsCodeKnown = true;
        }

        static string GetAclHash(string input)
        {
            //NOTE: if modifying this algo, update FastEndpoints.Endpoint.Base.GetAclHash() method also!
            using var sha256 = SHA256.Create();
            var base64Hash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(input.ToUpperInvariant())));

            var code = new char[3];
            var count = 0;

            for (var i = 0; i < base64Hash.Length && count < code.Length; i++)
            {
                if (char.IsLetterOrDigit(base64Hash[i]))
                    code[count++] = char.ToUpperInvariant(base64Hash[i]);
            }

            return new(code, 0, count);
        }
    }

}