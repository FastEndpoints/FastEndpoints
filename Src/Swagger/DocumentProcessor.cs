using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace FastEndpoints.Swagger;

sealed class DocumentProcessor : IDocumentProcessor
{
    const string StringSegmentKey = "MicrosoftExtensionsPrimitivesStringSegment";
    readonly int _maxEpVer;
    readonly int _minEpVer;
    readonly int _docRelVer;
    readonly bool _showDeprecated;

    public DocumentProcessor(int minEndpointVersion, int maxEndpointVersion, int releaseVersion, bool showDeprecatedOps)
    {
        _minEpVer = minEndpointVersion;
        _maxEpVer = maxEndpointVersion;
        _docRelVer = releaseVersion;
        _showDeprecated = showDeprecatedOps;

        switch (_docRelVer)
        {
            case > 0 when _minEpVer > 0:
                throw new NotSupportedException(
                    $"'{nameof(DocumentOptions.MinEndpointVersion)}' cannot be used together with '{nameof(DocumentOptions.ReleaseVersion)}'." +
                    $" Please choose a single strategy when defining a swagger document!");
            case > 0 when _maxEpVer > 0:
                throw new NotSupportedException(
                    $"'{nameof(DocumentOptions.MaxEndpointVersion)}' cannot be used together with '{nameof(DocumentOptions.ReleaseVersion)}'. " +
                    $"Please choose a single strategy when defining a swagger document");
        }

        if (_maxEpVer < _minEpVer)
            throw new ArgumentException($"{nameof(maxEndpointVersion)} must be greater than or equal to {nameof(minEndpointVersion)}");
    }

    static readonly string _isLatest = "__isLatest__";

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
                                       isFastEp = tagSegments?.Length > 0,
                                       route = tagSegments?[1],
                                       epVer = Convert.ToInt32(tagSegments?[2]),
                                       startingRelVer = Convert.ToInt32(tagSegments?[3]),
                                       depVer = Convert.ToInt32(tagSegments?[4]),
                                       pathItm = o.Parent,
                                       op = o
                                   };
                               })
                           .GroupBy(x => x.route)
                           .Select(
                               g =>
                               {
                                   var sortedGroup = g.OrderByDescending(x => x.epVer);
                                   var latestVersion = sortedGroup.FirstOrDefault(x => x.startingRelVer <= _docRelVer)?.epVer ?? 0;

                                   return new
                                   {
                                       pathItm = sortedGroup
                                                 .Where(
                                                     x => _docRelVer > 0
                                                              ? x.startingRelVer <= _docRelVer
                                                              : x.epVer >= _minEpVer && x.epVer <= _maxEpVer)
                                                 .Select(
                                                     x =>
                                                     {
                                                         if (x.isFastEp && x.epVer == latestVersion)
                                                             (x.op.ExtensionData ??= new Dictionary<string, object?>())[_isLatest] = "true";

                                                         return x;
                                                     })
                                                 .Take(_showDeprecated ? g.Count() : 1)
                                                 .Where(x => x.depVer == 0 || _showDeprecated || x.depVer > _maxEpVer)
                                                 .Select(x => x.pathItm)
                                   };
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
                var depVer = Convert.ToInt32(tagSegments?[4]);

                // var epVer = Convert.ToInt32(tagSegments?[2]);
                // var startingRelVer = Convert.ToInt32(tagSegments?[3]);
                // op.Value.Summary = $"epVer: {epVer} | startRelVer: {startingRelVer} | depVer: {depVer}";

                var isDeprecated = _docRelVer > 0
                                       ? (depVer > 0 && _docRelVer >= depVer) || op.Value.ExtensionData?.ContainsKey(_isLatest) is not true
                                       : _maxEpVer >= depVer && depVer != 0;

                op.Value.ExtensionData?.Remove(_isLatest);

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