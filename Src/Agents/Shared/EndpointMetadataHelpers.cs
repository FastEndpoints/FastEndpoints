namespace FastEndpoints.Agents;

static class EndpointMetadataHelpers
{
    internal static TMetadata? GetMetadata<TMetadata>(EndpointDefinition def) where TMetadata : class
    {
        if (def.EndpointMetadata is not { } meta)
            return null;

        foreach (var entry in meta)
        {
            if (entry is TMetadata metadata)
                return metadata;
        }

        return null;
    }

    internal static TMetadata GetOrCreateMetadata<TMetadata>(EndpointDefinition def) where TMetadata : class, new()
    {
        var metadata = GetMetadata<TMetadata>(def);

        if (metadata is not null)
            return metadata;

        metadata = new();
        def.Metadata(metadata);

        return metadata;
    }

    internal static TAttribute? GetAttribute<TAttribute>(EndpointDefinition def) where TAttribute : Attribute
    {
        if (def.EndpointAttributes is not { } attrs)
            return null;

        foreach (var attr in attrs)
        {
            if (attr is TAttribute attribute)
                return attribute;
        }

        return null;
    }
}
