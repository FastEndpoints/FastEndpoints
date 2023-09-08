using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FastEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public class AccessControlGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext ctx)
    {
        var syntaxProvider = ctx.SyntaxProvider.CreateSyntaxProvider(
            (sn, _) => sn is MethodDeclarationSyntax,
            (c, _) => Transform(c)
            ).Where(p => p is not null);

        var compilationAndClasses = ctx.CompilationProvider.Combine(syntaxProvider.Collect());

        ctx.RegisterSourceOutput(compilationAndClasses, (spc, src) => Execute(src.Right!, spc));
    }

    private Permission? Transform(GeneratorSyntaxContext ctx)
    {
        if (ctx.Node is MethodDeclarationSyntax n && n.Identifier.Value is "Configure")
        {
            _namespace = ctx.SemanticModel.Compilation.Assembly.Name;
            var endpoint = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node.Parent!)!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            foreach (var expStm in n.Body?.Statements.OfType<ExpressionStatementSyntax>() ?? Enumerable.Empty<ExpressionStatementSyntax>())
            {
                if (expStm.Expression is InvocationExpressionSyntax inv &&
                    inv.Expression is IdentifierNameSyntax id &&
                    id.Identifier.Value is "AccessControl")
                {
                    var args = inv.ArgumentList.Arguments
                        .OfType<ArgumentSyntax>()
                        .Select(a => a.Expression)
                        .OfType<LiteralExpressionSyntax>()
                        .Select(l => Sanitize(l.Token.ValueText));

                    return new(args.First(), endpoint, args.Skip(1));
                }
            }
        }
        return null;
    }

    private void Execute(ImmutableArray<Permission> perms, SourceProductionContext spc)
    {
        if (!perms.Any()) return;

        var groups = perms
            .SelectMany(perm => perm.Categories.Select(category => (category, name: perm.Name)))
            .OrderBy(x => x.name).ThenBy(x => x.category)
            .GroupBy(x => x.category, x => x.name)
            .ToDictionary(g => g.Key, g => g.Select(n => n));

        var fileContent = RenderClass(perms.OrderBy(p => p.Name), groups);
        spc.AddSource("Allow.g.cs", SourceText.From(fileContent, Encoding.UTF8));
    }

    private static string _namespace = default!;

    private static string RenderClass(IEnumerable<Permission> perms, Dictionary<string, IEnumerable<string>> groups)
    {
        var sb = new StringBuilder(@"#nullable enable

using System.Reflection;

namespace ").Append(_namespace).Append(@".Auth;

public static partial class Allow
{

#region ACL_ITEMS");
        foreach (var p in perms)
        {
            sb.Append(@"
    /// <summary><see cref=""").Append(p.Endpoint).Append(@"""/></summary>
    public const string ").Append(p.Name).Append(" = \"").Append(p.Code).Append(@""";
");
        }
        sb.Append(@"#endregion

");
        RenderGroups(sb, groups);
        sb.Append(@"

    private static readonly Dictionary<string, string> _perms = new();
    private static readonly Dictionary<string, string> _permsReverse = new();

    static Allow()
    {
        foreach (var f in typeof(Allow).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var val = f.GetValue(null)?.ToString() ?? string.Empty;
            _perms[f.Name] = val;
            _permsReverse[val] = f.Name;
        }
        Categorize();
    }

    /// <summary>
    /// implement this method to add custom permissions to the generated categories
    /// </summary>
    static partial void Categorize();

    /// <summary>
    /// gets a list of permission names for the given list of permission codes
    /// </summary>
    /// <param name=""codes"">the permission codes to get the permission names for</param>
    public static IEnumerable<string> NamesFor(IEnumerable<string> codes)
        => _perms.Where(kv => codes.Contains(kv.Value)).Select(kv => kv.Key);

    /// <summary>
    /// get a list of permission codes for a given list of permission names
    /// </summary>
    /// <param name=""names"">the permission names to get the codes for</param>
    public static IEnumerable<string> CodesFor(IEnumerable<string> names)
        => _perms.Where(kv => names.Contains(kv.Key)).Select(kv => kv.Value);

    /// <summary>
    /// get the permission code for a given permission name
    /// </summary>
    /// <param name=""permissionName"">the name of the permission to get the code for</param>
    public static string? PermissionCodeFor(string permissionName)
    {
        if (_perms.TryGetValue(permissionName, out var code))
            return code;
        return null;
    }

    /// <summary>
    /// get the permission name for a given permission code
    /// </summary>
    /// <param name=""permissionCode"">the permission code to get the name for</param>
    public static string? PermissionNameFor(string permissionCode)
    {
        if (_permsReverse.TryGetValue(permissionCode, out var name))
            return name;
        return null;
    }

    /// <summary>
    /// get a permission tuple using it's name. returns null if not found
    /// </summary>
    /// <param name=""permissionName"">name of the permission</param>
    public static (string PermissionName, string PermissionCode)? PermissionFromName(string permissionName)
    {
        if (_perms.TryGetValue(permissionName, out var code))
            return new(permissionName, code);
        return null;
    }

    /// <summary>
    /// get the permission tuple using it's code. returns null if not found
    /// </summary>
    /// <param name=""permissionCode"">code of the permission to get</param>
    public static (string PermissionName, string PermissionCode)? PermissionFromCode(string permissionCode)
    {
        if (_permsReverse.TryGetValue(permissionCode, out var name))
            return new(name, permissionCode);
        return null;
    }

    /// <summary>
    /// get a list of all permission names
    /// </summary>
    public static IEnumerable<string> AllNames()
        => _perms.Keys;

    /// <summary>
    /// get a list of all permission codes
    /// </summary>
    public static IEnumerable<string> AllCodes()
        => _perms.Values;

    /// <summary>
    /// get a list of all the defined permissions
    /// </summary>
    public static IEnumerable<(string PermissionName, string PermissionCode)> AllPermissions()
        => _perms.Select(kv => new ValueTuple<string, string>(kv.Key, kv.Value));
}");
        return sb.ToString();

        static void RenderGroups(StringBuilder sb, Dictionary<string, IEnumerable<string>> groups)
        {
            if (groups.Count > 0)
            {
                sb.Append("#region GROUPS");
                foreach (var g in groups)
                {
                    var key = $"_{g.Key.ToLower()}";

                    sb.Append(@"
    public static IEnumerable<string> ").Append(g.Key).Append(" => ").Append(key).Append(@";
    private static void AddTo").Append(g.Key).Append("(string permissionCode) => ").Append(key).Append(@".Add(permissionCode);
    private static readonly List<string> ").Append(key).Append(@" = new()
    {
");
                    foreach (var name in g.Value)
                    {
                        sb.Append("        ").Append(name).AppendLine(",");
                    }
                    sb.Remove(sb.Length - 2, 2).Append(@"
    };
");
                }
                sb.Append("#endregion");
            }
        }
    }

    private const string _replacement = "_";
    private static readonly Regex regex = new("[^a-zA-Z0-9]+", RegexOptions.Compiled);
    private static string Sanitize(string input)
        => regex.Replace(input, _replacement);

    private sealed class Permission
    {
        public string Name { get; }
        public string Code { get; }
        public string Endpoint { get; set; }
        public IEnumerable<string> Categories { get; set; }

        public Permission(string name, string endpoint, IEnumerable<string> categories)
        {
            Name = name;
            Code = GetAclHash(name);
            Endpoint = endpoint.Substring(8);
            Categories = categories;
        }

        private static string GetAclHash(string input)
        {
            //NOTE: if modifying this algo, update FastEndpoints.Endpoint.Base.ToAclKey() method also!
            using var sha256 = SHA256.Create();
            var base64Hash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(input.ToUpperInvariant())));
            return new(base64Hash.Where(char.IsLetterOrDigit).Take(3).Select(char.ToUpper).ToArray());
        }

    }
}