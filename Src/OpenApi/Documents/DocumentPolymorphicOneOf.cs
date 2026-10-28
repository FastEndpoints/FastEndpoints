using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class DocumentPolymorphicOneOf
{
    // discriminator mapping refs are unbound during schema transformation. bind oneOf to mapping ids that
    // are already components, and carry anyOf branches the mapping does not cover. promote inline anyOf
    // branches under those ids when the components are not stored yet. drop framework anyOf only after
    // every branch is represented. leave the schema unchanged when a branch ref is missing or inline
    // branches cannot be paired.
    public static void Apply(OpenApiDocument document)
    {
        if (document.Components?.Schemas is not { Count: > 0 } schemas)
            return;

        var promoted = false;
        var pending = new List<IOpenApiSchema>(schemas.Values);

        for (var index = 0; index < pending.Count; index++)
        {
            if (pending[index] is not OpenApiSchema concrete ||
                concrete.Discriminator?.Mapping is not { Count: > 0 } mapping ||
                concrete.OneOf is { Count: > 0 })
                continue;

            var ids = new List<string>(mapping.Count);
            var paired = true;

            foreach (var derived in mapping.Values)
            {
                if (derived.GetReferenceId() is not { Length: > 0 } id)
                {
                    paired = false;

                    break;
                }

                ids.Add(id);
            }

            if (!paired || ids.Count != mapping.Count)
                continue;

            if (ids.TrueForAll(schemas.ContainsKey))
            {
                TryBindExistingBranches(document, schemas, concrete, ids);

                continue;
            }

            if (TryPromoteInlineBranches(document, schemas, concrete, ids, pending))
                promoted = true;
        }

        if (promoted)
            document.SortSchemas();
    }

    static List<IOpenApiSchema> BindOneOf(OpenApiDocument host, List<string> ids)
    {
        var oneOf = new List<IOpenApiSchema>(ids.Count);

        foreach (var id in ids)
            oneOf.Add(new OpenApiSchemaReference(id, host));

        return oneOf;
    }

    static bool TryBindExistingBranches(
        OpenApiDocument host,
        IDictionary<string, IOpenApiSchema> schemas,
        OpenApiSchema concrete,
        List<string> ids)
    {
        var oneOf = BindOneOf(host, ids);

        if (concrete.AnyOf is { Count: > 0 } branches)
        {
            var seen = new HashSet<string>(ids, StringComparer.Ordinal);

            foreach (var branch in branches)
            {
                switch (branch)
                {
                    case OpenApiSchemaReference schemaRef:
                        if (schemaRef.GetReferenceId() is not { Length: > 0 } id || !schemas.ContainsKey(id))
                            return false;

                        if (seen.Add(id))
                            oneOf.Add(new OpenApiSchemaReference(id, host));

                        break;

                    case OpenApiSchema inline:
                        oneOf.Add(inline);

                        break;

                    default:
                        return false;
                }
            }
        }

        concrete.OneOf = oneOf;
        concrete.AnyOf = null;

        return true;
    }

    static bool TryPromoteInlineBranches(
        OpenApiDocument host,
        IDictionary<string, IOpenApiSchema> schemas,
        OpenApiSchema concrete,
        List<string> ids,
        List<IOpenApiSchema> pending)
    {
        if (concrete.AnyOf is not { Count: > 0 } branches || branches.Count != ids.Count)
            return false;

        var added = new List<string>(ids.Count);
        var stored = new List<OpenApiSchema>(ids.Count);

        for (var i = 0; i < ids.Count; i++)
        {
            if (schemas.ContainsKey(ids[i]) || branches[i] is not OpenApiSchema inline)
            {
                RemoveAdded(schemas, added);

                return false;
            }

            if (!schemas.TryAdd(ids[i], inline))
            {
                RemoveAdded(schemas, added);

                return false;
            }

            added.Add(ids[i]);
            stored.Add(inline);
        }

        concrete.OneOf = BindOneOf(host, ids);
        concrete.AnyOf = null;
        pending.AddRange(stored);

        return true;
    }

    static void RemoveAdded(IDictionary<string, IOpenApiSchema> schemas, List<string> added)
    {
        foreach (var id in added)
            schemas.Remove(id);
    }
}
