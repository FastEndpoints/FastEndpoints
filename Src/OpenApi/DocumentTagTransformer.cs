using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class DocumentTagTransformer
{
    public static void Apply(OpenApiDocument document, DocumentOptions opts)
    {
        if (opts.TagDescriptions is null)
            return;

        var dict = new Dictionary<string, string>();
        opts.TagDescriptions(dict);

        document.Tags ??= new HashSet<OpenApiTag>();
        var existingTags = document.Tags
                                   .Where(static tag => tag.Name is not null)
                                   .ToDictionary(static tag => tag.Name!, StringComparer.Ordinal);

        foreach (var kvp in dict)
        {
            if (existingTags.TryGetValue(kvp.Key, out var tag))
            {
                tag.Description = kvp.Value;

                continue;
            }

            tag = new() { Name = kvp.Key, Description = kvp.Value };
            if (document.Tags.Add(tag))
                existingTags[kvp.Key] = tag;
        }
    }

    public static void Cleanup(OpenApiDocument document, DocumentOptions opts)
    {
        if (opts.TagDescriptions is not null || document.Tags is not { Count: > 0 } tags)
            return;

        var emptyTags = tags.Where(static tag => !HasTagMetadata(tag)).ToArray();

        foreach (var tag in emptyTags)
            tags.Remove(tag);

        static bool HasTagMetadata(OpenApiTag tag)
            => !string.IsNullOrEmpty(tag.Description) ||
               tag.ExternalDocs is not null ||
               tag.Extensions is { Count: > 0 };
    }
}