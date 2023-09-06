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
            foreach (var expStm in n.Body?.Statements.OfType<ExpressionStatementSyntax>() ?? Enumerable.Empty<ExpressionStatementSyntax>())
            {
                if (expStm.Expression is InvocationExpressionSyntax inv &&
                    inv.Expression is IdentifierNameSyntax id &&
                    id.Identifier.Value is "AccessControlKey")
                {
                    _namespace = ctx.SemanticModel.Compilation.Assembly.Name;
                    var keyName = ((LiteralExpressionSyntax)inv.ArgumentList.Arguments.First().Expression).Token.Value!.ToString();
                    return new(keyName);
                }
            }
        }
        return null;
    }

    private void Execute(ImmutableArray<Permission> perms, SourceProductionContext spc)
    {
        if (!perms.Any()) return;
        var fileContent = GetContent(perms.OrderBy(p => p.Name));
        spc.AddSource("Allow.g.cs", SourceText.From(fileContent, Encoding.UTF8));
    }

    private static string _namespace = default!;

    private static string GetContent(IEnumerable<Permission> perms)
    {
        var sb = new StringBuilder(@"using System.Reflection;

namespace ").Append(_namespace).Append(@".Auth;

public static partial class Allow
{
    private static readonly Dictionary<string, string> _perms = new();
    private static readonly Dictionary<string, string> _permsReverse = new();
");
        foreach (var p in perms)
        {
            sb.Append(@$"
    public const string {p.Name} = ""{p.Code}"";");
        }
        sb.Append(@"

    static Allow()
    {
        foreach (var f in typeof(Allow).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var val = f.GetValue(null)?.ToString() ?? string.Empty;
            _perms[f.Name] = val;
            _permsReverse[val] = f.Name;
        }
    }

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
    }

    private sealed class Permission
    {
        public string Name { get; }
        public string Code { get; }

        public Permission(string name)
        {
            Name = Sanitize(name);
            Code = GetCode(name);
        }

        private static readonly SHA256 sha256 = SHA256.Create();
        private static string GetCode(string input)
        {
            var base64Hash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(input.ToUpperInvariant())));
            return new(base64Hash.Where(char.IsLetterOrDigit).Take(3).Select(c => char.ToUpper(c)).ToArray());
        }

        private const string _replacement = "_";
        private static readonly Regex regex = new("[^a-zA-Z0-9]+", RegexOptions.Compiled);
        private static string Sanitize(string input)
            => regex.Replace(input, _replacement);
    }
}