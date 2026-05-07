using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed partial class OperationMetadataTransformer(DocumentOptions docOpts, SharedContext sharedCtx)
{
        readonly OperationAutoTagApplicator _autoTagApplicator = new(docOpts);
        readonly OperationHeaderMetadataApplicator _headerMetadataApplicator = new(docOpts, sharedCtx);
        readonly OperationSecurityRequirementBuilder _securityRequirementBuilder = new(docOpts);

        public void ApplyAutoTag(OpenApiOperation operation, EndpointDefinition epDef, string bareRoute, IList<object> metadata)
            => _autoTagApplicator.Apply(operation, epDef, bareRoute, metadata);

        public void AddIdempotencyHeader(OpenApiOperation operation, EndpointDefinition epDef)
            => _headerMetadataApplicator.AddIdempotencyHeader(operation, epDef);

        public void AddX402Headers(OpenApiOperation operation, EndpointDefinition epDef)
            => _headerMetadataApplicator.AddX402Headers(operation, epDef);

        public void ApplySecurityRequirements(OpenApiOperation operation, EndpointDefinition? epDef, IList<object> metadata, string operationKey)
        {
            var authorizeAttributes = new List<AuthorizeAttribute>();
            var hasAllowAnonymous = false;

            for (var i = 0; i < metadata.Count; i++)
            {
                switch (metadata[i])
                {
                    case AllowAnonymousAttribute:
                        hasAllowAnonymous = true;

                        break;
                    case AuthorizeAttribute authorizeAttribute:
                        authorizeAttributes.Add(authorizeAttribute);

                        break;
                }
            }

            if (hasAllowAnonymous || authorizeAttributes.Count == 0)
            {
                operation.Security?.Clear();

                return;
            }

            var securityEntries = _securityRequirementBuilder.Build(epDef, authorizeAttributes);

            if (securityEntries.Length > 0)
                sharedCtx.SecurityRequirements[operationKey] = securityEntries;
        }

}
