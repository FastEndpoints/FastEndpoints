using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace FastEndpoints.Swagger;

internal class DefaultDocumentProcessor : IDocumentProcessor
{
    private readonly int maxEpVer;
    public DefaultDocumentProcessor(int maxEndpointVersion) => maxEpVer = maxEndpointVersion;

    public void Process(DocumentProcessorContext ctx)
    {
        var pathItems = ctx.Document.Paths
            .SelectMany(p => p.Value.Values)
            .Select(o =>
            {
                var tag = o.Tags.Single(t => t.StartsWith("|"));
                var segments = tag.Split("|");
                return new
                {
                    ver = Convert.ToInt32(segments[2]),
                    route = segments[1],
                    pathItm = o.Parent
                };
            })
            .GroupBy(x => x.route)
            .Select(g => new
            {
                pathItm = g.Where(x => x.ver <= maxEpVer)
                           .OrderByDescending(x => x.ver)
                           .Take(1)
                           .Select(x => x.pathItm)
            })
            .SelectMany(x => x.pathItm)
            .ToArray();

        foreach (var p in ctx.Document.Paths)
        {
            if (!pathItems.Contains(p.Value))
                ctx.Document.Paths.Remove(p.Key);

            foreach (var op in p.Value.Values)
                op.Tags.Remove(op.Tags.Single(t => t.StartsWith("|")));
        }
    }
}