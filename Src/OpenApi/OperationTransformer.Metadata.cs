using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed partial class OperationTransformer
{
    sealed partial class OperationMetadataTransformer(DocumentOptions docOpts, SharedContext sharedCtx)
    {
        static readonly TextInfo _textInfo = CultureInfo.InvariantCulture.TextInfo;

        public void ApplyAutoTag(OpenApiOperation operation, EndpointDefinition epDef, string bareRoute, IList<object> metadata)
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

        public void AddIdempotencyHeader(OpenApiOperation operation, EndpointDefinition epDef)
        {
            if (epDef.IdempotencyOptions is null)
                return;

            if (HasParameter(operation, ParameterLocation.Header, epDef.IdempotencyOptions.HeaderName))
                return;

            operation.Parameters ??= [];
            var exampleValue = epDef.IdempotencyOptions.SwaggerExampleGenerator?.Invoke();
            var exampleNode = exampleValue.JsonNodeFromObject();

            operation.Parameters.Add(
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

        public void AddX402Headers(OpenApiOperation operation, EndpointDefinition epDef)
        {
            if (epDef.X402PaymentMetadata is null)
                return;

            if (!HasParameter(operation, ParameterLocation.Header, X402Constants.PaymentSignatureHeader))
            {
                operation.Parameters ??= [];
                operation.Parameters.Add(
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

                concreteResponse.Headers ??= new Dictionary<string, IOpenApiHeader>();

                if (statusCode == "402")
                {
                    concreteResponse.Headers[X402Constants.PaymentRequiredHeader] = new OpenApiHeader
                    {
                        Description = "Base64-encoded x402 payment challenge payload.",
                        Schema = OperationSchemaHelpers.StringSchema()
                    };
                }

                concreteResponse.Headers[X402Constants.PaymentResponseHeader] = new OpenApiHeader
                {
                    Description = "Base64-encoded x402 settlement result. Present when the middleware attempts settlement.",
                    Schema = OperationSchemaHelpers.StringSchema()
                };
            }
        }

        public void ApplySecurityRequirements(OpenApiOperation operation, EndpointDefinition epDef, IList<object> metadata, string operationKey)
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

            var scopes = BuildScopes(authorizeAttributes);
            var securityEntries = BuildSecurityEntries(epDef, scopes);

            if (securityEntries.Length > 0)
                sharedCtx.SecurityRequirements[operationKey] = securityEntries;
        }

        (string SchemeName, string[] Scopes)[] BuildSecurityEntries(EndpointDefinition epDef, IReadOnlyCollection<string> scopes)
        {
            var securityEntries = new List<(string SchemeName, string[] Scopes)>();

            foreach (var authConfig in docOpts.AuthSchemes)
            {
                var epSchemes = epDef.AuthSchemeNames;

                if (epSchemes?.Contains(authConfig.Name) == false)
                    continue;

                var mergedScopes = new HashSet<string>(scopes, StringComparer.Ordinal);

                if (authConfig.GlobalScopes is not null)
                {
                    foreach (var scope in authConfig.GlobalScopes)
                        mergedScopes.Add(scope);
                }

                securityEntries.Add((authConfig.Name, [.. mergedScopes]));
            }

            return [.. securityEntries];
        }

        static string[] BuildScopes(IEnumerable<AuthorizeAttribute> authorizeAttributes)
        {
            var scopes = new HashSet<string>(StringComparer.Ordinal);

            foreach (var authorizeAttribute in authorizeAttributes)
            {
                if (authorizeAttribute.Roles is not { Length: > 0 } roles)
                    continue;

                foreach (var role in roles.Split(','))
                    scopes.Add(role);
            }

            return [.. scopes];
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
}