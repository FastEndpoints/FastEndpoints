// using System.Text;
// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.CSharp.Syntax;
// using Microsoft.CodeAnalysis.Text;
//
// namespace FastEndpoints.Generator;
//
// [Generator(LanguageNames.CSharp)]
// public class RequestBinderGenerator : IIncrementalGenerator
// {
//     // ReSharper disable once InconsistentNaming
//     static readonly StringBuilder b = new();
//
//     public void Initialize(IncrementalGeneratorInitializationContext ctx)
//     {
//         var syntaxProvider = ctx.SyntaxProvider
//                                 .CreateSyntaxProvider(Match, Transform)
//                                 .Where(static t => t is not null);
//
//         ctx.RegisterSourceOutput(syntaxProvider, Generate);
//
//         static bool Match(SyntaxNode node, CancellationToken _)
//             => node is ClassDeclarationSyntax { TypeParameterList: null } cds &&
//                cds.Modifiers.Any(t => t.ValueText == "partial") &&
//                !cds.AttributeLists.Any(l => l.Attributes.Any(s => s.Name is IdentifierNameSyntax { Identifier.ValueText : "GeneratedCode" }));
//
//         static EndpointInfo? Transform(GeneratorSyntaxContext ctx, CancellationToken _)
//         {
//             var tEndpoint = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is not ITypeSymbol type || type.IsAbstract || type.AllInterfaces.Length == 0
//                                 ? null
//                                 : type.AllInterfaces.Any(i => i.ToDisplayString() == "FastEndpoints.IEndpoint")
//                                     ? type
//                                     : null;
//
//             if (tEndpoint is null)
//                 return null;
//
//             return new(tEndpoint);
//         }
//     }
//
//     void Generate(SourceProductionContext spc, EndpointInfo? epInfo)
//     {
//         if (epInfo is null)
//             return;
//
//         var fileContent = RenderClass(epInfo);
//         spc.AddSource($"{epInfo.EndpointName}Binder.g.cs", SourceText.From(fileContent, Encoding.UTF8));
//     }
//
//     string RenderClass(EndpointInfo ep)
//     {
//         b.Clear().w("using System.CodeDom.Compiler;");
//
//         if (!ep.IsGlobalNamespace)
//         {
//             b.w(
//                 $"""
//
//
//                  namespace {ep.Namespace};
//
//                  """);
//         }
//
//         b.w(
//             $"""
//
//              [GeneratedCode(null, null)]
//              {ep.EndpointModifier}partial class {ep.EndpointName}
//
//              """);
//
//         b.w(
//             $$"""
//               {
//                   void GenerateRequestBinder()
//                   {
//                       //blah bhla
//                   }
//
//                   sealed class {{ep.DtoName}}Binder: IRequestBinder<{{ep.DtoType}}>
//                   {
//                       public async ValueTask<{{ep.DtoType}}> BindAsync(BinderContext ctx, CancellationToken ct)
//                       {
//                           return await ValueTask.FromResult(new {{ep.DtoType}}());
//                       }
//                   }
//               }
//               """);
//
//         return b.ToString();
//     }
//
//     sealed class EndpointInfo : IEquatable<EndpointInfo>
//     {
//         public string Namespace { get; }
//         public bool IsGlobalNamespace { get; }
//         public string EndpointName { get; }
//         public string EndpointModifier { get; }
//         public string DtoType { get; }
//         public string DtoName { get; }
//
//         readonly int _hash; //used as Roslyn cache key
//
//         internal EndpointInfo(ITypeSymbol endpointType)
//         {
//             var dtoType = endpointType.BaseType!.TypeArguments[0];
//
//             Namespace = endpointType.ContainingNamespace.ToDisplayString();
//             IsGlobalNamespace = endpointType.ContainingNamespace.IsGlobalNamespace;
//             EndpointName = endpointType.Name;
//             EndpointModifier = endpointType.DeclaredAccessibility == Accessibility.Public ? "public " : string.Empty;
//             DtoType = dtoType.ToDisplayString();
//             DtoName = dtoType.Name;
//
//             unchecked
//             {
//                 _hash = 17;
//                 _hash = _hash * 31 + endpointType.ToDisplayString().GetHashCode();
//                 _hash = _hash * 31 + dtoType.ToDisplayString().GetHashCode();
//
//                 // foreach (var category in Categories)
//                 //     _hash = _hash * 31 + category.GetHashCode();
//             }
//         }
//
//         public bool Equals(EndpointInfo other)
//             => other._hash.Equals(_hash);
//     }
// }