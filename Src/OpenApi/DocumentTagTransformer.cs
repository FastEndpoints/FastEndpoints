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

        foreach (var kvp in dict)
        {
            var existing = document.Tags.FirstOrDefault(t => t.Name == kvp.Key);
            if (existing is not null)
                existing.Description = kvp.Value;
            else
                document.Tags.Add(new() { Name = kvp.Key, Description = kvp.Value });
        }
    }

    public static void Cleanup(OpenApiDocument document, DocumentOptions opts)
    {
        if (opts.TagDescriptions is not null || document.Tags is null)
            return;

        foreach (var tag in document.Tags.Where(static t => string.IsNullOrEmpty(t.Description) && t.ExternalDocs is null && t.Extensions is not { Count: > 0 }).ToArray())
            document.Tags.Remove(tag);
    }
}
