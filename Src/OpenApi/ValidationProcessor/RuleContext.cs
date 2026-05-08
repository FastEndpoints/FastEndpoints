// Original: https://github.com/zymlabs/nswag-fluentvalidation
// MIT License
// Copyright (c) 2019 Zym Labs LLC

using FluentValidation.Validators;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi.ValidationProcessor;

[HideFromDocs]
public class RuleContext(OpenApiSchema schema, string propertyKey, IPropertyValidator propertyValidator, bool hasCondition, OpenApiSchema? propertySchema = null)
{
    public OpenApiSchema Schema { get; } = schema;
    public string PropertyKey { get; } = propertyKey;
    public IPropertyValidator PropertyValidator { get; } = propertyValidator;
    public bool HasCondition { get; set; } = hasCondition;

    /// <summary>
    /// tries to resolve the concrete <see cref="OpenApiSchema" /> for <see cref="PropertyKey" /> from <see cref="Schema" />.
    /// </summary>
    public bool TryGetPropertySchema(out OpenApiSchema propertySchema1)
    {
        if (propertySchema is not null)
        {
            propertySchema1 = propertySchema;

            return true;
        }

        if (Schema.Properties?.TryGetValue(PropertyKey, out var p) == true && p.ResolveSchema() is { } s)
        {
            propertySchema1 = s;

            return true;
        }

        propertySchema1 = null!;

        return false;
    }
}