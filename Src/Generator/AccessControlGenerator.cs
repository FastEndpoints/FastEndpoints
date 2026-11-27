using System.Collections.Immutable;
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

    //identity of the generated Allow class. hand-written members are only picked up from a class matching it.
    const string AllowClass = "Allow";

    static string AuthNamespace(string rootNamespace)
        => $"{rootNamespace}.Auth";

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
                             .Where(static p => p is not null)
                             .Collect();

        // picks up hand-written members of partial Allow class declarations: public const/static string fields,
        // and partial property declarations for which the generator provides the implementing half with a computed hash code
        var allowMembers = initCtx.SyntaxProvider
                                  .CreateSyntaxProvider(static (n, ct) => QualifyCustom(n, ct) || QualifyPartial(n, ct), TransformAllowMember)
                                  .Where(static p => p is not null)
                                  .Collect();

        initCtx.RegisterSourceOutput(matches.Combine(allowMembers).Combine(rootNamespace), Generate);

        //executed per each keystroke
        //AccessControl() is protected, so it can only be called bare or via 'this.' / 'base.'
        static bool Qualify(SyntaxNode node, CancellationToken _)
            => node is InvocationExpressionSyntax
            {
                ArgumentList.Arguments.Count: not 0,
                Expression: IdentifierNameSyntax { Identifier.ValueText: AccessControl } or
                            MemberAccessExpressionSyntax
                            {
                                Expression: ThisExpressionSyntax or BaseExpressionSyntax, Name.Identifier.ValueText: AccessControl
                            }
            };

        //executed per each keystroke but only for syntax nodes filtered by the Qualify method
        static Permission? Transform(GeneratorSyntaxContext ctx, CancellationToken _)
        {
            var invocation = (InvocationExpressionSyntax)ctx.Node;

            //skip same-named user methods. unresolved calls (broken code while typing) are kept.
            var symbolInfo = ctx.SemanticModel.GetSymbolInfo(invocation);
            var methods = symbolInfo.Symbol is { } symbol ? [symbol] : symbolInfo.CandidateSymbols;

            if (methods.Length > 0 && !methods.Any(static m => m.ContainingNamespace?.ToDisplayString() == "FastEndpoints"))
                return null;

            //the call can be nested in blocks, lambdas, local functions etc. of the endpoint class
            if (invocation.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } typeDeclaration ||
                ctx.SemanticModel.GetDeclaredSymbol(typeDeclaration) is not { } endpoint)
                return null;

            var arguments = invocation.ArgumentList.Arguments;
            var permissionArg = arguments.FirstOrDefault(static a => a.NameColon?.Name.Identifier.ValueText == "keyName") ?? arguments[0];

            if (TryGetStringConstant(ctx.SemanticModel, permissionArg.Expression) is not { } permissionName)
                return null;

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
               IsPublicMemberOfAllow(field) &&
               (field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)) || field.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)));

        //executed per each keystroke
        static bool QualifyPartial(SyntaxNode node, CancellationToken _)
            => node is PropertyDeclarationSyntax prop &&
               IsPublicMemberOfAllow(prop) &&
               prop.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)) &&
               prop.Modifiers.Any(m => m.ValueText == "partial") &&
               prop is { AccessorList.Accessors.Count: 1 } &&
               prop.AccessorList.Accessors[0].IsKind(SyntaxKind.GetAccessorDeclaration) &&
               prop.AccessorList.Accessors[0].Body is null &&
               prop.AccessorList.Accessors[0].ExpressionBody is null &&
               prop.ExpressionBody is null;

        static bool IsPublicMemberOfAllow(MemberDeclarationSyntax member)
            => member.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)) &&
               member.Parent is ClassDeclarationSyntax { Identifier.ValueText: AllowClass };

        //executed per each keystroke but only for syntax nodes filtered by the QualifyCustom or QualifyPartial methods
        static AllowMember? TransformAllowMember(GeneratorSyntaxContext ctx, CancellationToken _)
        {
            var member = ctx.Node switch
            {
                VariableDeclaratorSyntax variable => ctx.SemanticModel.GetDeclaredSymbol(variable),
                PropertyDeclarationSyntax prop => ctx.SemanticModel.GetDeclaredSymbol(prop),
                _ => null
            };

            if (member is not (IFieldSymbol { Type.SpecialType: SpecialType.System_String } or IPropertySymbol { Type.SpecialType: SpecialType.System_String }) ||
                !IsGeneratedAllowType(member.ContainingType, ctx.SemanticModel.Compilation) ||
                member.HasAttribute($"{nameof(FastEndpoints)}.{nameof(HideFromDocsAttribute)}"))
                return null;

            //a field keeps its own value (a null code when it's only known at runtime). a partial property gets a computed code.
            return member is IFieldSymbol field
                       ? new(member.Name, field.ConstantValue as string, IsPartialProperty: false)
                       : new(member.Name, AclHash.Compute(member.Name), IsPartialProperty: true);
        }

        static bool IsGeneratedAllowType(INamedTypeSymbol? type, Compilation compilation)
            => type is { Name: AllowClass, IsStatic: true } &&
               type.ContainingNamespace.ToDisplayString().Equals(AuthNamespace(compilation.ToRootNamespace()), StringComparison.Ordinal);
    }

    //only executed if the equality comparer says the data is not what has been cached by roslyn
    static void Generate(SourceProductionContext spc, ((ImmutableArray<Permission?>, ImmutableArray<AllowMember?>), string) input)
    {
        var ((matches, allowMembers), rootNamespace) = input;

        if (matches.Length == 0 && allowMembers.Length == 0)
            return;

        var endpointPerms = matches.Select(static p => p!).OrderBy(static p => p.Name, StringComparer.Ordinal).ToArray();

        var sortedAllowMembers = allowMembers.Select(static m => m!)
                                             .OrderBy(static m => m.IsPartialProperty)
                                             .ThenBy(static m => m.Name, StringComparer.Ordinal)
                                             .ToArray();

        //keep this order (endpoint permissions, then fields, then partial properties, each by name).
        //the lookups are filled by indexer assignment, so the last entry wins on a name or code clash.
        AllowMember[] allPerms = [.. endpointPerms.Select(static p => new AllowMember(p.Name, p.Code, IsPartialProperty: false)), .. sortedAllowMembers];

        ReportDuplicatePermissionCodes(spc, allPerms);

        spc.AddSource(
            "Allow.g.cs",
            SourceText.From(
                RenderClass(rootNamespace, endpointPerms, [.. sortedAllowMembers.Where(static m => m.IsPartialProperty)], allPerms),
                Encoding.UTF8));
    }

    static void ReportDuplicatePermissionCodes(SourceProductionContext spc, AllowMember[] perms)
    {
        var duplicates = perms.Where(static p => p.Code is not null)
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

    static string RenderClass(string rootNamespace, Permission[] endpointPerms, AllowMember[] partialPerms, AllowMember[] allPerms)
    {
        var b = new StringBuilder().w(
            $$"""
              {{RenderAllowHeader(rootNamespace, frozen: false)}}

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

        RenderInitPermissions(b, allPerms);

        RenderGroups(b, endpointPerms);

        RenderDescriptions(b, endpointPerms);

        b.w(
            """

            }
            """);

        return b.ToString();

        static void RenderPartialFields(StringBuilder sb, AllowMember[] perms)
        {
            if (perms.Length == 0)
                return;

            sb.w(
                """

                #region PARTIAL_ACL_ITEMS
                """);

            //partial properties always get a computed code (see TransformAllowMember)
            foreach (var p in perms)
            {
                sb.w(
                    $$"""

                          public static partial string {{p.Name.EscapeKeyword()}} { get => {{CsString(p.Code!)}}; }
                      """);
            }

            sb.w(
                """

                #endregion

                """);
        }

        static void RenderInitPermissions(StringBuilder sb, AllowMember[] perms)
        {
            sb.w(
                """

                    static partial void InitPermissions(Dictionary<string, string> permNames, Dictionary<string, string> permCodes)
                    {

                """);

            foreach (var p in perms)
            {
                //a null code is read from the hand-written field at runtime. qualified so a field can't be shadowed by the parameters.
                var code = p.Code is { } c ? CsString(c) : $"{AllowClass}.{p.Name.EscapeKeyword()} ?? string.Empty";

                sb.w(
                    $"""
                             permNames[{CsString(p.Name)}] = {code};
                             permCodes[{code}] = {CsString(p.Name)};

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

    //the start of every generated part of the Allow class, up to its opening brace
    static string RenderAllowHeader(string rootNamespace, bool frozen)
    {
        var frozenUsing = frozen ? "\nusing System.Collections.Frozen;" : null;

        return $$"""
                 #nullable enable

                 using FastEndpoints;
                 using System;{{frozenUsing}}
                 using System.Collections.Generic;
                 using System.Linq;

                 namespace {{AuthNamespace(rootNamespace)}};

                 public static partial class {{AllowClass}}
                 {
                 """;
    }

    static string RenderBase(string rootNamespace)
        => $$"""
             {{RenderAllowHeader(rootNamespace, frozen: true)}}
                 private static readonly FrozenDictionary<string, string> _permNames;
                 private static readonly FrozenDictionary<string, string> _permCodes;

                 static {{AllowClass}}()
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

    //a permission found in an endpoint's AccessControl(...) call
    sealed record Permission
    {
        public string Endpoint { get; }
        public string Name { get; }
        public string Code { get; }
        public EquatableArray<string> Categories { get; }
        public string? Description { get; }

        internal Permission(string endpoint, string permissionName, string[] groupNames, string? description)
        {
            Endpoint = endpoint;
            Name = permissionName.ToValidCSharpIdentifier();
            Code = AclHash.Compute(permissionName);
            Categories = new([.. groupNames.Select(static c => c.ToValidCSharpIdentifier())]);
            Description = description;
        }
    }

    //a hand-written Allow member (field or partial property). the code is null when it's only known at runtime (a non-const field).
    sealed record AllowMember(string Name, string? Code, bool IsPartialProperty);
}