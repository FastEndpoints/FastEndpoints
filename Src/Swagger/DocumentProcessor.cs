using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace FastEndpoints.Swagger;

internal sealed class DocumentProcessor : IDocumentProcessor
{
    private readonly int maxEpVer;
    private readonly int minEpVer;

    public DocumentProcessor(int minEndpointVersion, int maxEndpointVersion)
    {
        minEpVer = minEndpointVersion;
        maxEpVer = maxEndpointVersion;

        if (maxEpVer < minEpVer)
            throw new ArgumentException($"{nameof(maxEndpointVersion)} must be greater than or equal to {nameof(minEndpointVersion)}");
    }

    public void Process(DocumentProcessorContext ctx)
    {
        var pathItems = ctx.Document.Paths
            .SelectMany(p => p.Value.Values)
            .Select(o =>
            {
                var tag = o.Tags.SingleOrDefault(t => t.StartsWith("|"));
                var segments = tag?.Split("|");
                return new {
                    route = segments?[1],
                    ver = Convert.ToInt32(segments?[2]),
                    depVer = Convert.ToInt32(segments?[3]),
                    pathItm = o.Parent
                };
            })
            .GroupBy(x => x.route)
            .Select(g => new {
                pathItm = g.Where(x => x.ver >= minEpVer && x.ver <= maxEpVer)
                           .OrderByDescending(x => x.ver)
                           .Take(1)
                           .Where(x => x.depVer == 0 || x.depVer > maxEpVer)
                           .Select(x => x.pathItm)
            })
            .SelectMany(x => x.pathItm)
            .ToArray();

        foreach (var p in ctx.Document.Paths)
        {
            var isFastEp = p.Value.SelectMany(o => o.Value.Tags).Any(t => t.StartsWith("|"));

            if (!isFastEp)
                continue; //this isn't a fastendpoint. so don't remove it from the paths

            if (!pathItems.Contains(p.Value))
                ctx.Document.Paths.Remove(p.Key);

            foreach (var op in p.Value.Values)
                op.Tags.Remove(op.Tags.SingleOrDefault(t => t.StartsWith("|")));
        }
    }
}