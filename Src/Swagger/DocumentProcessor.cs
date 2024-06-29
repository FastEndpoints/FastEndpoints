using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace FastEndpoints.Swagger;

sealed class DocumentProcessor : IDocumentProcessor
{
    const string StringSegmentKey = "MicrosoftExtensionsPrimitivesStringSegment";
    readonly int _maxEpVer;
    readonly int _minEpVer;
    readonly bool _showDeprecated;

    public DocumentProcessor(int minEndpointVersion, int maxEndpointVersion, bool showDeprecatedOps)
    {
        _minEpVer = minEndpointVersion;
        _maxEpVer = maxEndpointVersion;
        _showDeprecated = showDeprecatedOps;

        if (_maxEpVer < _minEpVer)
            throw new ArgumentException($"{nameof(maxEndpointVersion)} must be greater than or equal to {nameof(minEndpointVersion)}");
    }

    public void Process(DocumentProcessorContext ctx)
    {
        var pathItems = ctx.Document.Paths
                           .SelectMany(p => p.Value.Values)
                           .Select(
                               o =>
                               {
                                   var tagSegments = o.Tags.SingleOrDefault(t => t.StartsWith("|"))?.Split("|");

                                   return new
                                   {
                                       route = tagSegments?[1],
                                       ver = Convert.ToInt32(tagSegments?[2]),
                                       depVer = Convert.ToInt32(tagSegments?[3]),
                                       pathItm = o.Parent
                                   };
                               })
                           .GroupBy(x => x.route)
                           .Select(
                               g => new
                               {
                                   pathItm = g.Where(x => x.ver >= _minEpVer && x.ver <= _maxEpVer)
                                              .OrderByDescending(x => x.ver)
                                              .Take(_showDeprecated ? g.Count() : 1)
                                              .Where(x => x.depVer == 0 || _showDeprecated || x.depVer > _maxEpVer)
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

            foreach (var op in p.Value)
            {
                var tagSegments = op.Value.Tags.SingleOrDefault(t => t.StartsWith("|"))?.Split("|");
                var depVer = Convert.ToInt32(tagSegments?[3]);
                var isDeprecated = _maxEpVer >= depVer && depVer != 0;

                if (isDeprecated && _showDeprecated)
                    op.Value.IsDeprecated = isDeprecated;

                if (isDeprecated && !_showDeprecated)
                    p.Value.Remove(op.Key);

                op.Value.Tags.Remove(op.Value.Tags.SingleOrDefault(t => t.StartsWith("|")));
            }
        }

        var schemas = ctx.Document.Components.Schemas;

        foreach (var s in schemas)
        {
            var headerRemoved = false;

            if (s.Key.EndsWith("HeaderValue"))
            {
                schemas.Remove(s.Key);
                headerRemoved = true;
            }

            if (headerRemoved && schemas.ContainsKey(StringSegmentKey))
                schemas.Remove(StringSegmentKey);
        }
    }
}