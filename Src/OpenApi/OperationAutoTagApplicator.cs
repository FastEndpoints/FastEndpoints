using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed partial class OperationAutoTagApplicator(DocumentOptions docOpts)
{
    static readonly TextInfo _textInfo = CultureInfo.InvariantCulture.TextInfo;

    internal void Apply(OpenApiOperation operation, EndpointDefinition epDef, string bareRoute, IList<object> metadata)
    {
        HashSet<string>? explicitTags = null;
        string? overrideVal = null;

        for (var i = 0; i < metadata.Count; i++)
        {
            switch (metadata[i])
            {
                case ITagsMetadata tagsMetadata:
                {
                    explicitTags ??= new(StringComparer.OrdinalIgnoreCase);

                    foreach (var tagName in tagsMetadata.Tags)
                        explicitTags.Add(tagName);

                    break;
                }
                case AutoTagOverride autoTagOverride:
                    overrideVal = autoTagOverride.TagName;

                    break;
            }
        }

        // always strip framework-generated tags (controller/assembly name) that weren't set via WithTags
        if (operation.Tags is { Count: > 0 })
        {
            foreach (var t in operation.Tags.ToArray())
            {
                if (t.Name is null || explicitTags?.Contains(t.Name) is not true)
                    operation.Tags.Remove(t);
            }
        }

        if (docOpts.AutoTagPathSegmentIndex <= 0 || epDef.DontAutoTagEndpoints)
            return;

        string? tag = null;

        if (overrideVal is not null)
            tag = TagName(overrideVal, docOpts.TagCase, docOpts.TagStripSymbols);
        else
        {
            var segments = bareRoute.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= docOpts.AutoTagPathSegmentIndex)
                tag = TagName(segments[docOpts.AutoTagPathSegmentIndex - 1], docOpts.TagCase, docOpts.TagStripSymbols);
        }

        if (tag is not null)
        {
            operation.Tags ??= new HashSet<OpenApiTagReference>();
            operation.Tags.Add(new(tag));
        }
    }

    static string TagName(string input, TagCase tagCase, bool stripSymbols)
    {
        return StripSymbols(
            tagCase switch
            {
                TagCase.None => input,
                TagCase.TitleCase => _textInfo.ToTitleCase(input),
                TagCase.LowerCase => _textInfo.ToLower(input),
                _ => input
            });

        string StripSymbols(string val)
            => stripSymbols ? TagSymbolsRegex().Replace(val, "") : val;
    }

    [GeneratedRegex("[^a-zA-Z0-9]")]
    private static partial Regex TagSymbolsRegex();
}
