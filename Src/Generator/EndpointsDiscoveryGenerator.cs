using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace FastEndpoints.Generator
{
    [Generator]
    public class EndpointsDiscoveryGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var excludes = new[]
            {
                "Microsoft.",
                "System.",
                "FastEndpoints.",
                "testhost",
                "netstandard",
                "Newtonsoft.",
                "mscorlib",
                "NuGet.",
                "NSwag."
            };

            var discoveredTypes = AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(a =>
                    !a.IsDynamic &&
                    !excludes.Any(n => a.FullName!.StartsWith(n)))
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                    !t.IsAbstract &&
                    !t.IsInterface &&
                    !t.IsGenericType &&
                    t.GetInterfaces().Intersect(new[] {
                        t.GetInterface("IEndpoint"),
                        t.GetInterface("IValidator"),
                        t.GetInterface("IEventHandler"),
                        t.GetInterface("ISummary")
                    }).Any());

            var sourceBuilder = new StringBuilder(@"using System;
namespace FastEndpoints
{
    public static class DiscoveredTypes
    {
        public static readonly System.Type[] AllTypes = new System.Type[]
        {");

            foreach (var discoveredType in discoveredTypes)
            {
                sourceBuilder.Append(@$"
            typeof({discoveredType.Namespace}.{discoveredType.Name}),
");
            }

            sourceBuilder.Append(@"
        };
    }
}
");

            context.AddSource("FastEndpointsDiscoveredTypesGenerated", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
        }
    }
}
