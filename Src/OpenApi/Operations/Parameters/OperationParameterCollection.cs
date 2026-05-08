using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class OperationParameterCollection
{
    internal static bool Has(OpenApiOperation operation, ParameterLocation location, string name)
        => Find(operation, location, name) is not null;

    internal static OpenApiParameter? Find(OpenApiOperation operation, ParameterLocation location, string name)
    {
        if (operation.Parameters is not { Count: > 0 })
            return null;

        foreach (var param in operation.Parameters)
        {
            if (param is OpenApiParameter concreteParam &&
                concreteParam.In == location &&
                string.Equals(concreteParam.Name, name, StringComparison.OrdinalIgnoreCase))
                return concreteParam;
        }

        return null;
    }

    internal static void Add(OpenApiOperation operation, OpenApiParameter parameter)
    {
        operation.Parameters ??= [];
        operation.Parameters.Add(parameter);
    }

    internal static void UpdateSchema(OpenApiOperation operation, ParameterLocation location, string name, Type type, SharedContext sharedCtx, bool shortSchemaNames)
    {
        var param = Find(operation, location, name);

        if (param is not null)
            param.Schema = type.GetSchemaForType(sharedCtx, shortSchemaNames);
    }
}
