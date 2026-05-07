using System.Reflection;
using System.Text.Json;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed class OperationParameterFactory(DocumentOptions docOpts, SharedContext sharedCtx)
{
    JsonNamingPolicy? NamingPolicy => sharedCtx.NamingPolicy;
    JsonSerializerOptions SerializerOptions => sharedCtx.SerializerOptions ?? Cfg.SerOpts.Options;

    internal OpenApiParameter Create(string name,
                                     ParameterLocation location,
                                     PropertyInfo? prop,
                                     bool? isRequired = null,
                                     bool shortSchemaNames = false,
                                     Type? explicitType = null)
    {
        var schemaInfo = CreateSchemaInfo(prop, explicitType, isRequired, shortSchemaNames);

        var param = new OpenApiParameter
        {
            Name = name,
            In = location,
            Required = schemaInfo.Required,
            Schema = schemaInfo.Schema
        };

        if (ShouldUseJsonContent(location, schemaInfo.Type))
        {
            param.Schema = null;
            param.Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new() { Schema = schemaInfo.Schema }
            };
        }

        if (schemaInfo.Required && prop is not null)
            ApplyRequiredExample(param, prop, schemaInfo.Type);

        return param;
    }

    ParameterSchemaInfo CreateSchemaInfo(PropertyInfo? prop, Type? explicitType, bool? isRequired, bool shortSchemaNames)
    {
        var propType = GetParameterType(prop, explicitType);
        var schema = propType.GetSchemaForType(sharedCtx, shortSchemaNames);

        if (schema is OpenApiSchema concreteSchema)
            OperationSchemaHelpers.ApplyUniqueItems(concreteSchema, propType, prop);

        var isNullable = prop is not null && OperationTransformer.IsNullable(prop);
        var hasCtorDefault = prop?.GetParentCtorDefaultValue() is not null;
        var required = isRequired ?? (!hasCtorDefault && !isNullable);

        return new(propType, schema, required);
    }

    static Type GetParameterType(PropertyInfo? prop, Type? explicitType)
    {
        var propType = explicitType ?? prop?.PropertyType ?? typeof(string);

        // typed header values (e.g. ContentDispositionHeaderValue) are transmitted as strings
        return propType.Name.EndsWith("HeaderValue", StringComparison.Ordinal) ? typeof(string) : propType;
    }

    void ApplyRequiredExample(OpenApiParameter param, PropertyInfo prop, Type propType)
    {
        var example = OperationSchemaHelpers.ParseXmlExampleJsonNode(XmlDocLookup.GetPropertyExample(prop)) ??
                      propType.GenerateSampleJsonNode(SerializerOptions, NamingPolicy, docOpts.UsePropertyNamingPolicy);

        if (example is null)
            return;

        if (param.Content is not null)
            param.Content["application/json"].Example = example;
        else
            param.Example = example;
    }

    static bool ShouldUseJsonContent(ParameterLocation location, Type type)
    {
        if (location != ParameterLocation.Query)
            return false;

        type = Nullable.GetUnderlyingType(type) ?? type;

        return (type.IsComplexType() && !type.IsCollection()) ||
               OperationSchemaHelpers.TryGetDictionaryValueType(type) is not null;
    }

    readonly record struct ParameterSchemaInfo(Type Type, IOpenApiSchema? Schema, bool Required);
}
