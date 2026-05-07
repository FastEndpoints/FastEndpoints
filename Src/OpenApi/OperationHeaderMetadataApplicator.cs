using System.Text.Json;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed class OperationHeaderMetadataApplicator(DocumentOptions docOpts, SharedContext sharedCtx)
{
    JsonSerializerOptions SerializerOptions => sharedCtx.SerializerOptions ?? Cfg.SerOpts.Options;

    internal void AddIdempotencyHeader(OpenApiOperation operation, EndpointDefinition epDef)
    {
        if (epDef.IdempotencyOptions is null)
            return;

        if (OperationParameterCollection.Has(operation, ParameterLocation.Header, epDef.IdempotencyOptions.HeaderName))
            return;

        var exampleValue = epDef.IdempotencyOptions.SwaggerExampleGenerator?.Invoke();
        var exampleNode = exampleValue.JsonNodeFromObject(SerializerOptions);

        OperationParameterCollection.Add(
            operation,
            new OpenApiParameter
            {
                Name = epDef.IdempotencyOptions.HeaderName,
                In = ParameterLocation.Header,
                Required = true,
                Description = epDef.IdempotencyOptions.SwaggerHeaderDescription,
                Schema = epDef.IdempotencyOptions.SwaggerHeaderType is not null
                             ? epDef.IdempotencyOptions.SwaggerHeaderType.GetSchemaForType(sharedCtx, docOpts.ShortSchemaNames)
                             : OperationSchemaHelpers.CreateSchemaFromExampleNode(exampleNode) ?? OperationSchemaHelpers.StringSchema(),
                Example = exampleNode
            });
    }

    internal void AddX402Headers(OpenApiOperation operation, EndpointDefinition epDef)
    {
        if (epDef.X402PaymentMetadata is null)
            return;

        if (!OperationParameterCollection.Has(operation, ParameterLocation.Header, X402Constants.PaymentSignatureHeader))
        {
            OperationParameterCollection.Add(
                operation,
                new OpenApiParameter
                {
                    Name = X402Constants.PaymentSignatureHeader,
                    In = ParameterLocation.Header,
                    Required = false,
                    Description = "Base64-encoded x402 payment payload.",
                    Schema = OperationSchemaHelpers.StringSchema()
                });
        }

        if (operation.Responses is null)
            return;

        foreach (var (statusCode, response) in operation.Responses)
        {
            if (response is not OpenApiResponse concreteResponse)
                continue;

            if (statusCode == "402")
            {
                concreteResponse.AddHeader(
                    X402Constants.PaymentRequiredHeader,
                    new()
                    {
                        Description = "Base64-encoded x402 payment challenge payload.",
                        Schema = OperationSchemaHelpers.StringSchema()
                    });
            }

            concreteResponse.AddHeader(
                X402Constants.PaymentResponseHeader,
                new()
                {
                    Description = "Base64-encoded x402 settlement result. Present when the middleware attempts settlement.",
                    Schema = OperationSchemaHelpers.StringSchema()
                });
        }
    }
}
