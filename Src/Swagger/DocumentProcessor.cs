using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace FastEndpoints.Swagger;

internal sealed class DocumentProcessor : IDocumentProcessor
{
    private readonly int maxEpVer;
    private readonly int minEpVer;
    private readonly bool showDeprecated;

    public DocumentProcessor(int minEndpointVersion, int maxEndpointVersion, bool showDeprecatedOps)
    {
        minEpVer = minEndpointVersion;
        maxEpVer = maxEndpointVersion;
        showDeprecated = showDeprecatedOps;

        if (maxEpVer < minEpVer)
            throw new ArgumentException($"{nameof(maxEndpointVersion)} must be greater than or equal to {nameof(minEndpointVersion)}");
    }

    public void Process(DocumentProcessorContext ctx)
    {
        var pathItems = ctx.Document.Paths
            .SelectMany(p => p.Value.Values)
            .Select(o =>
            {
                var tagSegments = o.Tags.SingleOrDefault(t => t.StartsWith("|"))?.Split("|");
                return new {
                    route = tagSegments?[1],
                    ver = Convert.ToInt32(tagSegments?[2]),
                    depVer = Convert.ToInt32(tagSegments?[3]),
                    pathItm = o.Parent
                };
            })
            .GroupBy(x => x.route)
            .Select(g => new {
                pathItm = g.Where(x => x.ver >= minEpVer && x.ver <= maxEpVer)
                           .OrderByDescending(x => x.ver)
                           .Take(showDeprecated ? g.Count() : 1)
                           .Where(x => x.depVer == 0 || showDeprecated || x.depVer > maxEpVer)
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
            {
                if (showDeprecated)
                {
                    var tagSegments = op.Tags.SingleOrDefault(t => t.StartsWith("|"))?.Split("|");
                    var depVer = Convert.ToInt32(tagSegments?[3]);
                    op.IsDeprecated = maxEpVer >= depVer && depVer != 0;
                }
                op.Tags.Remove(op.Tags.SingleOrDefault(t => t.StartsWith("|")));
            }
        }
    }
}