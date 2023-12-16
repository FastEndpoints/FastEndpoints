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
    static string? _assemblyName;

    // ReSharper disable once InconsistentNaming
    static readonly StringBuilder b = new();

    public void Initialize(IncrementalGeneratorInitializationContext ctx)
    {
        var assemblyName = ctx.CompilationProvider.Select(static (c, _) => c.AssemblyName);

        ctx.RegisterSourceOutput(
            assemblyName,
            static (spc, assembly) => spc.AddSource("Allow.b.g.cs", SourceText.From(RenderBase(assembly), Encoding.UTF8)));

        var permissions = ctx.SyntaxProvider
                             .CreateSyntaxProvider(Match, Transform)
                             .Where(static p => p is not null)
                             .Collect();

        ctx.RegisterSourceOutput(permissions, Generate!);

        static bool Match(SyntaxNode node, CancellationToken _)
            => node is InvocationExpressionSyntax { ArgumentList.Arguments.Count: not 0, Expression: IdentifierNameSyntax { Identifier.ValueText: "AccessControl" } };

        static Permission? Transform(GeneratorSyntaxContext ctx, CancellationToken _)
        {
            _assemblyName = ctx.SemanticModel.Compilation.AssemblyName;

            var endpoint = ctx.SemanticModel
                              .GetDeclaredSymbol(ctx.Node.Parent!.Parent!.Parent!.Parent!)?
                              .ToDisplayString();

            if (endpoint is null)
                return null;

            var inv = (InvocationExpressionSyntax)ctx.Node;

            var args = inv.ArgumentList.Arguments
                          .Select(a => a.Expression)
                          .OfType<LiteralExpressionSyntax>()
                          .Select(l => l.Token.ValueText.Sanitize());

            var desc = inv.ArgumentList.OpenParenToken.TrailingTrivia.SingleOrDefault(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia)).ToString();

            return new(endpoint, args.First(), desc, args.Skip(1));
        }
    }

    static void Generate(SourceProductionContext spc, ImmutableArray<Permission> perms)
    {
        if (!perms.Any())
            return;

        var groups = perms
                     .SelectMany(perm => perm.Categories.Select(category => (category, name: perm.Name)))
                     .OrderBy(x => x.name).ThenBy(x => x.category)
                     .GroupBy(x => x.category, x => x.name)
                     .ToDictionary(g => g.Key, g => g.Select(name => name));

        var fileContent = RenderClass(perms.OrderBy(p => p.Name), groups);
        spc.AddSource("Allow.g.cs", SourceText.From(fileContent, Encoding.UTF8));
    }

    static string RenderClass(IEnumerable<Permission> perms, Dictionary<string, IEnumerable<string>> groups)
    {
        b.Clear().w(
            $$"""
              #nullable enable

              namespace {{_assemblyName}}.Auth;

              public static partial class Allow
              {

              #region ACL_ITEMS
              """);

        foreach (var p in perms)
        {
            b.w(
                $"""
                 
                     /// <summary>{p.Description}</summary><remark>Generated from endpoint: <see cref="{p.Endpoint}"/></remark>
                     public const string {p.Name} = "{p.Code}";
                 """);
        }
        b.w(
            """

            #endregion

            """);

        RenderGroups(b, groups);

        RenderDescriptions(b, perms);
        b.w(
            """

            }
            """);

        return b.ToString();

        static void RenderGroups(StringBuilder sb, Dictionary<string, IEnumerable<string>> groups)
        {
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
                                  { "{{p.Code}}", "{{p.Description}}" },
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

             using System.Reflection;

             namespace {{assemblyName}}.Auth;

             public static partial class Allow
             {
                 private static readonly Dictionary<string, string> _permNames = new();
                 private static readonly Dictionary<string, string> _permCodes = new();
             
                 static Allow()
                 {
                     foreach (var f in typeof(Allow).GetFields(BindingFlags.Public | BindingFlags.Static))
                     {
                         var val = f.GetValue(null)?.ToString() ?? string.Empty;
                         _permNames[f.Name] = val;
                         _permCodes[val] = f.Name;
                     }
                     Groups();
                     Describe();
                 }
             
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

    sealed class Permission : IEquatable<Permission>
    {
        public string Name { get; }
        public string? Description { get; }
        public string Code { get; }
        public string Endpoint { get; }
        public IEnumerable<string> Categories { get; }

        readonly int _hash; //used as Roslyn cache key

        internal Permission(string endpoint, string name, string description, IEnumerable<string> categories)
        {
            Name = name.Length == 0 ? "Unspecified" : name;
            Description = description.Length == 0 ? null : description.Substring(2).Trim();
            Code = GetAclHash(name);
            Endpoint = endpoint;
            Categories = categories;

            unchecked
            {
                _hash = 17;
                _hash = _hash * 31 + Name.GetHashCode();
                _hash = _hash * 31 + Endpoint.GetHashCode();

                foreach (var category in Categories)
                    _hash = _hash * 31 + category.GetHashCode();
            }
        }

        static readonly SHA256 _sha256 = SHA256.Create();

        static string GetAclHash(string input)
        {
            //NOTE: if modifying this algo, update FastEndpoints.Endpoint.Base.ToAclKey() method also!
            var base64Hash = Convert.ToBase64String(_sha256.ComputeHash(Encoding.UTF8.GetBytes(input.ToUpperInvariant())));

            return new(base64Hash.Where(char.IsLetterOrDigit).Take(3).Select(char.ToUpper).ToArray());
        }

        public bool Equals(Permission other)
            => other._hash.Equals(_hash);
    }
}