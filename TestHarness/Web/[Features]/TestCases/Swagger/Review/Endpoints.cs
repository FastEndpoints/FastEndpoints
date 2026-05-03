using FluentValidation;
using FastEndpoints.OpenApi;
using Microsoft.OpenApi;

namespace TestCases.Swagger.Review;

sealed class AutoTagOverrideEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/swagger-review/auto-tag-override");
        Tags("swagger_review");
        AllowAnonymous();
        Description(b => b.AutoTagOverride("review-tag"));
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync("ok", ct);
}

sealed class DuplicateRequestExamplesRequest
{
    public string Name { get; set; } = string.Empty;
}

sealed class DuplicateRequestExamplesEndpoint : Endpoint<DuplicateRequestExamplesRequest, string>
{
    public override void Configure()
    {
        Post("/swagger-review/duplicate-examples");
        Tags("swagger_review");
        AllowAnonymous();
        Summary(
            s =>
            {
                s.RequestExamples.Add(new(new DuplicateRequestExamplesRequest { Name = "first" }));
                s.RequestExamples.Add(new(new DuplicateRequestExamplesRequest { Name = "second" }));
            });
    }

    public override Task HandleAsync(DuplicateRequestExamplesRequest req, CancellationToken ct)
        => Send.OkAsync(req.Name, ct);
}

sealed class SharedRequestMetadataReviewRequest
{
    public string Name { get; set; } = string.Empty;
}

sealed class SharedRequestMetadataAlphaEndpoint : Endpoint<SharedRequestMetadataReviewRequest, string>
{
    public override void Configure()
    {
        Post("/swagger-review/shared-request-metadata-alpha");
        Tags("swagger_review");
        AllowAnonymous();
        Summary(
            s =>
            {
                s.Params[nameof(SharedRequestMetadataReviewRequest.Name)] = "alpha description";
                s.ExampleRequest = new() { Name = "alpha example" };
            });
    }

    public override Task HandleAsync(SharedRequestMetadataReviewRequest req, CancellationToken ct)
        => Send.OkAsync(req.Name, ct);
}

sealed class SharedRequestMetadataBetaEndpoint : Endpoint<SharedRequestMetadataReviewRequest, string>
{
    public override void Configure()
    {
        Post("/swagger-review/shared-request-metadata-beta");
        Tags("swagger_review");
        AllowAnonymous();
        Summary(
            s =>
            {
                s.Params[nameof(SharedRequestMetadataReviewRequest.Name)] = "beta description";
                s.ExampleRequest = new() { Name = "beta example" };
            });
    }

    public override Task HandleAsync(SharedRequestMetadataReviewRequest req, CancellationToken ct)
        => Send.OkAsync(req.Name, ct);
}

sealed class SharedNestedValidationAddress
{
    public string Zip { get; set; } = string.Empty;
}

sealed class SharedNestedValidationAlphaRequest
{
    public SharedNestedValidationAddress Address { get; set; } = new();
}

sealed class SharedNestedValidationBetaRequest
{
    public SharedNestedValidationAddress Address { get; set; } = new();
}

sealed class SharedNestedValidationAlphaValidator : Validator<SharedNestedValidationAlphaRequest>
{
    public SharedNestedValidationAlphaValidator()
    {
        RuleFor(x => x.Address.Zip).NotEmpty();
    }
}

sealed class SharedNestedValidationAlphaEndpoint : Endpoint<SharedNestedValidationAlphaRequest, string>
{
    public override void Configure()
    {
        Post("/swagger-review/shared-nested-validation-alpha");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(SharedNestedValidationAlphaRequest req, CancellationToken ct)
        => Send.OkAsync(req.Address.Zip, ct);
}

sealed class SharedNestedValidationBetaEndpoint : Endpoint<SharedNestedValidationBetaRequest, string>
{
    public override void Configure()
    {
        Post("/swagger-review/shared-nested-validation-beta");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(SharedNestedValidationBetaRequest req, CancellationToken ct)
        => Send.OkAsync(req.Address.Zip, ct);
}

sealed class VersionPrefilterSharedRequest
{
    public string Name { get; set; } = string.Empty;
}

sealed class VersionPrefilterInitialEndpoint : Endpoint<VersionPrefilterSharedRequest, string>
{
    public override void Configure()
    {
        Post("/swagger-review/version-prefilter-initial");
        Tags("swagger_review");
        AllowAnonymous();
        Summary(s => s.Params[nameof(VersionPrefilterSharedRequest.Name)] = "initial description");
    }

    public override Task HandleAsync(VersionPrefilterSharedRequest req, CancellationToken ct)
        => Send.OkAsync(req.Name, ct);
}

sealed class VersionPrefilterV1Endpoint : Endpoint<VersionPrefilterSharedRequest, string>
{
    public override void Configure()
    {
        Post("/swagger-review/version-prefilter-v1");
        Tags("swagger_review");
        Version(1);
        AllowAnonymous();
    }

    public override Task HandleAsync(VersionPrefilterSharedRequest req, CancellationToken ct)
        => Send.OkAsync(req.Name, ct);
}

sealed class EmptySchemaCleanupRequest
{
    [FromHeader("x-review-header")]
    public string HeaderValue { get; set; } = string.Empty;

    [HideFromDocs]
    public string HiddenValue { get; set; } = string.Empty;
}

sealed class EmptySchemaCleanupEndpoint : Endpoint<EmptySchemaCleanupRequest, string>
{
    public override void Configure()
    {
        Post("/swagger-review/empty-schema-cleanup");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(EmptySchemaCleanupRequest req, CancellationToken ct)
        => Send.OkAsync(req.HeaderValue, ct);
}

sealed class HiddenSchemaReviewRequest
{
    public string VisibleValue { get; set; } = string.Empty;

    [HideFromDocs]
    public string HiddenValue { get; set; } = string.Empty;

    [JsonIgnore]
    public string IgnoredValue { get; set; } = string.Empty;
}

sealed class HiddenSchemaReviewResponse
{
    public string VisibleValue { get; set; } = string.Empty;

    [HideFromDocs]
    public string HiddenValue { get; set; } = string.Empty;
}

sealed class HiddenSchemaReviewEndpoint : Endpoint<HiddenSchemaReviewRequest, HiddenSchemaReviewResponse>
{
    public override void Configure()
    {
        Post("/swagger-review/hidden-schema");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(HiddenSchemaReviewRequest req, CancellationToken ct)
        => Send.OkAsync(new() { VisibleValue = req.VisibleValue }, ct);
}

sealed class IllegalHeadersRequest
{
    [FromHeader("Accept", IsRequired = false)]
    public string? Accept { get; set; }

    [FromHeader("Authorization", IsRequired = false)]
    public string? Authorization { get; set; }

    [FromHeader("Content-Type", IsRequired = false)]
    public string? ContentType { get; set; }

    public string BodyValue { get; set; } = string.Empty;
}

sealed class IllegalHeadersEndpoint : Endpoint<IllegalHeadersRequest, string>
{
    public override void Configure()
    {
        Post("/swagger-review/illegal-headers");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(IllegalHeadersRequest req, CancellationToken ct)
        => Send.OkAsync(req.BodyValue, ct);
}

sealed class SharedPathFastEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Post("/filtered-shared-path");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync("fast-endpoint-post", ct);
}

sealed class BareRouteSubstringReviewEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/apiary/ver0/status");
        RoutePrefixOverride(string.Empty);
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync("ok", ct);
}

sealed class CatchAllRouteReviewRequest
{
    public string Slug { get; set; } = string.Empty;
}

sealed class CatchAllRouteReviewEndpoint : Endpoint<CatchAllRouteReviewRequest, string>
{
    public override void Configure()
    {
        Get("/swagger-review/catch-all/{*slug}");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(CatchAllRouteReviewRequest req, CancellationToken ct)
        => Send.OkAsync(req.Slug, ct);
}

sealed class DuplicateQueryNamingPolicyRequest
{
    public string FirstName { get; set; } = string.Empty;
}

sealed class DuplicateQueryNamingPolicyEndpoint : Endpoint<DuplicateQueryNamingPolicyRequest, string>
{
    public override void Configure()
    {
        Get("/swagger-review/duplicate-query-naming-policy");
        Tags("swagger_review");
        AllowAnonymous();
        Description(
            b => b.WithMetadata(
                new OpenApiOperation
                {
                    Parameters =
                    [
                        new OpenApiParameter
                        {
                            Name = "firstName",
                            In = ParameterLocation.Query,
                            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                        }
                    ]
                }));
    }

    public override Task HandleAsync(DuplicateQueryNamingPolicyRequest req, CancellationToken ct)
        => Send.OkAsync(req.FirstName, ct);
}

sealed class BindFromQueryGetReviewRequest
{
    [BindFrom("id")]
    public string CustomerID { get; set; } = string.Empty;
}

sealed class BindFromQueryGetReviewEndpoint : Endpoint<BindFromQueryGetReviewRequest, string>
{
    public override void Configure()
    {
        Get("/swagger-review/bindfrom-query-get");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(BindFromQueryGetReviewRequest req, CancellationToken ct)
        => Send.OkAsync(req.CustomerID, ct);
}

sealed class BindFromQueryPostReviewRequest
{
    [QueryParam, BindFrom("id")]
    public string CustomerID { get; set; } = string.Empty;

    public string BodyValue { get; set; } = string.Empty;
}

sealed class BindFromQueryPostReviewEndpoint : Endpoint<BindFromQueryPostReviewRequest, string>
{
    public override void Configure()
    {
        Post("/swagger-review/bindfrom-query-post");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(BindFromQueryPostReviewRequest req, CancellationToken ct)
        => Send.OkAsync(req.BodyValue, ct);
}

sealed class JsonNamedQueryMetadataReviewRequest
{
    [JsonPropertyName("customer_id"), System.ComponentModel.DefaultValue("default-customer")]
    public string CustomerId { get; set; } = string.Empty;
}

sealed class JsonNamedQueryMetadataReviewEndpoint : Endpoint<JsonNamedQueryMetadataReviewRequest, string>
{
    public override void Configure()
    {
        Get("/swagger-review/json-named-query-metadata");
        Tags("swagger_review");
        AllowAnonymous();
        Summary(
            s =>
            {
                s.Params[nameof(JsonNamedQueryMetadataReviewRequest.CustomerId)] = "customer id query summary";
                s.ExampleRequest = new() { CustomerId = "example-customer" };
            });
    }

    public override Task HandleAsync(JsonNamedQueryMetadataReviewRequest req, CancellationToken ct)
        => Send.OkAsync(req.CustomerId, ct);
}

sealed class DefaultValueSchemaReviewRequest
{
    [System.ComponentModel.DefaultValue("schema-default")]
    public string Name { get; set; } = string.Empty;

    [System.ComponentModel.DefaultValue(7)]
    public int Count { get; set; }
}

sealed class DefaultValueSchemaReviewEndpoint : Endpoint<DefaultValueSchemaReviewRequest, string>
{
    public override void Configure()
    {
        Post("/swagger-review/default-value-schema");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(DefaultValueSchemaReviewRequest req, CancellationToken ct)
        => Send.OkAsync(req.Name, ct);
}

sealed class RequiredQueryParamReviewRequest
{
    [QueryParam(IsRequired = true)]
    public string? Search { get; set; }

    [QueryParam]
    public string? Filter { get; set; }

    public string BodyValue { get; set; } = string.Empty;
}

sealed class RequiredQueryParamReviewEndpoint : Endpoint<RequiredQueryParamReviewRequest, string>
{
    public override void Configure()
    {
        Post("/swagger-review/required-query-param");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(RequiredQueryParamReviewRequest req, CancellationToken ct)
        => Send.OkAsync(req.Search ?? req.BodyValue, ct);
}

sealed class PromotedBodyValidationReviewRequest
{
    public int Id { get; set; }

    [FromBody]
    public PromotedBodyValidationPayload Body { get; set; } = new();
}

sealed class PromotedBodyValidationPayload
{
    public string? Name { get; set; }

    public PromotedBodyValidationChild Child { get; set; } = new();
}

sealed class PromotedBodyValidationChild
{
    public string? Code { get; set; }
}

sealed class UniqueItemsReviewRequest
{
    public HashSet<string> AutoTags { get; set; } = [];

    public HashSet<UniqueItemsReviewChild> AutoChildren { get; set; } = [];

    [UniqueItems]
    public List<UniqueItemsReviewChild> ExplicitChildren { get; set; } = [];
}

sealed class UniqueItemsReviewResponse
{
    public SortedSet<int> AutoIds { get; set; } = [];

    [UniqueItems]
    public List<UniqueItemsReviewChild> ExplicitChildren { get; set; } = [];
}

sealed class UniqueItemsReviewChild
{
    public string? Name { get; set; }
}

sealed class UniqueItemsReviewEndpoint : Endpoint<UniqueItemsReviewRequest, UniqueItemsReviewResponse>
{
    public override void Configure()
    {
        Post("/swagger-review/unique-items");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(UniqueItemsReviewRequest req, CancellationToken ct)
        => Send.OkAsync(
            new()
            {
                AutoIds = [1, 2],
                ExplicitChildren = req.ExplicitChildren
            },
            ct);
}

sealed class PromotedBodyValidationChildValidator : Validator<PromotedBodyValidationChild>
{
    public PromotedBodyValidationChildValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MinimumLength(2);
    }
}

sealed class PromotedBodyValidationPayloadValidator : Validator<PromotedBodyValidationPayload>
{
    public PromotedBodyValidationPayloadValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(3);
        RuleFor(x => x.Child).SetValidator(new PromotedBodyValidationChildValidator());
    }
}

sealed class PromotedBodyValidationReviewValidator : Validator<PromotedBodyValidationReviewRequest>
{
    public PromotedBodyValidationReviewValidator()
    {
        RuleFor(x => x.Body).SetValidator(new PromotedBodyValidationPayloadValidator());
    }
}

sealed class PromotedBodyValidationReviewEndpoint : Endpoint<PromotedBodyValidationReviewRequest, string>
{
    public override void Configure()
    {
        Post("/swagger-review/promoted-body-validation/{id}");
        Tags("swagger_review");
        AllowAnonymous();
        Summary(
            s => s.ExampleRequest = new()
            {
                Id = 123,
                Body = new()
                {
                    Name = "example name",
                    Child = new() { Code = "xy" }
                }
            });
    }

    public override Task HandleAsync(PromotedBodyValidationReviewRequest req, CancellationToken ct)
        => Send.OkAsync(req.Body.Name ?? string.Empty, ct);
}

sealed class CookieGetReviewRequest
{
    [FromCookie("session_id")]
    public string SessionId { get; set; } = string.Empty;
}

sealed class CookieGetReviewEndpoint : Endpoint<CookieGetReviewRequest, string>
{
    public override void Configure()
    {
        Get("/swagger-review/cookie-get");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(CookieGetReviewRequest req, CancellationToken ct)
        => Send.OkAsync(req.SessionId, ct);
}

sealed class IdempotencyAnonymousExampleEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Post("/swagger-review/idempotency-anonymous-example");
        Tags("swagger_review");
        AllowAnonymous();
        Idempotency(
            o =>
            {
                o.SwaggerHeaderDescription = "custom idempotency header";
                o.SwaggerExampleGenerator = () => new { key = "demo-key", scope = "tenant-a" };
            });
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync("ok", ct);
}

sealed class DuplicateIdempotencyHeaderRequest
{
    [FromHeader("Idempotency-Key")]
    public string IdempotencyKey { get; set; } = string.Empty;
}

sealed class DuplicateIdempotencyHeaderEndpoint : Endpoint<DuplicateIdempotencyHeaderRequest, string>
{
    public override void Configure()
    {
        Post("/swagger-review/duplicate-idempotency-header");
        Tags("swagger_review");
        AllowAnonymous();
        Idempotency();
    }

    public override Task HandleAsync(DuplicateIdempotencyHeaderRequest req, CancellationToken ct)
        => Send.OkAsync(req.IdempotencyKey, ct);
}

sealed class DuplicateX402HeaderRequest
{
    [FromHeader("PAYMENT-SIGNATURE", IsRequired = false)]
    public string? PaymentSignature { get; set; }
}

sealed class DuplicateX402HeaderEndpoint : Endpoint<DuplicateX402HeaderRequest, string>
{
    public override void Configure()
    {
        Get("/swagger-review/duplicate-x402-header");
        Tags("swagger_review");
        AllowAnonymous();
        RequirePayment("1000", "Protected review endpoint");
    }

    public override Task HandleAsync(DuplicateX402HeaderRequest req, CancellationToken ct)
        => Send.OkAsync(req.PaymentSignature ?? "", ct);
}

sealed class ManualSchemaNested
{
    public string Value { get; set; } = string.Empty;
}

sealed class ManualSchemaQueryRequest
{
    public ManualSchemaNested Filter { get; set; } = new();
}

sealed class ManualSchemaResponse
{
    [ToHeader("x-complex-header")]
    public ManualSchemaNested Header { get; set; } = new();

    public string BodyValue { get; set; } = string.Empty;
}

sealed class ManualSchemaIdempotencyHeader
{
    public string Key { get; set; } = string.Empty;
}

sealed class ManualSchemaQueryEndpoint : Endpoint<ManualSchemaQueryRequest, string>
{
    public override void Configure()
    {
        Get("/swagger-review/manual-complex-query");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(ManualSchemaQueryRequest req, CancellationToken ct)
        => Send.OkAsync(req.Filter.Value, ct);
}

sealed class ManualSchemaResponseHeaderEndpoint : EndpointWithoutRequest<ManualSchemaResponse>
{
    public override void Configure()
    {
        Get("/swagger-review/manual-complex-response-header");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(new() { BodyValue = "ok", Header = new() { Value = "header" } }, ct);
}

sealed class ManualSchemaIdempotencyHeaderEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Post("/swagger-review/manual-complex-idempotency-header");
        Tags("swagger_review");
        AllowAnonymous();
        Idempotency(
            o =>
            {
                o.SwaggerHeaderType = typeof(ManualSchemaIdempotencyHeader);
                o.SwaggerExampleGenerator = () => new ManualSchemaIdempotencyHeader { Key = "demo-key" };
            });
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync("ok", ct);
}

sealed class ChildValidatorReviewRequest
{
    public ChildValidatorReviewChild Child { get; set; } = new();
}

sealed class ChildValidatorReviewChild
{
    public int Score { get; set; }
}

sealed class ChildValidatorReviewChildValidator : Validator<ChildValidatorReviewChild>
{
    public ChildValidatorReviewChildValidator()
    {
        RuleFor(x => x.Score).GreaterThan(10);
    }
}

sealed class ChildValidatorReviewValidator : Validator<ChildValidatorReviewRequest>
{
    public ChildValidatorReviewValidator()
    {
        RuleFor(x => x.Child).SetValidator(new ChildValidatorReviewChildValidator());
    }
}

sealed class ChildValidatorReviewEndpoint : Endpoint<ChildValidatorReviewRequest, string>
{
    public override void Configure()
    {
        Post("/swagger-review/child-validator");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(ChildValidatorReviewRequest req, CancellationToken ct)
        => Send.OkAsync(req.Child.Score.ToString(), ct);
}

sealed class DeepNestedValidatorReviewRequest
{
    public DeepNestedValidatorReviewChild Child { get; set; } = new();
}

sealed class DeepNestedValidatorReviewChild
{
    public DeepNestedValidatorReviewGrandChild SubChild { get; set; } = new();
}

sealed class DeepNestedValidatorReviewGrandChild
{
    public string Field { get; set; } = string.Empty;
}

sealed class DeepNestedValidatorReviewValidator : Validator<DeepNestedValidatorReviewRequest>
{
    public DeepNestedValidatorReviewValidator()
    {
        RuleFor(x => x.Child.SubChild.Field).MinimumLength(5);
    }
}

sealed class DeepNestedValidatorReviewEndpoint : Endpoint<DeepNestedValidatorReviewRequest, string>
{
    public override void Configure()
    {
        Post("/swagger-review/deep-nested-validator");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(DeepNestedValidatorReviewRequest req, CancellationToken ct)
        => Send.OkAsync(req.Child.SubChild.Field, ct);
}

sealed class JsonPropertyNameTransformerReviewRequest
{
    [JsonPropertyName("x_coord")]
    public int XCoord { get; set; }
}

sealed class JsonPropertyNameTransformerReviewResponse
{
    /// <summary>
    /// secret header summary
    /// </summary>
    /// <example>xml-secret-header</example>
    [JsonPropertyName("x_secret"), ToHeader("x-secret")]
    public string Secret { get; set; } = string.Empty;

    public string BodyValue { get; set; } = string.Empty;
}

sealed class JsonPropertyNameTransformerReviewValidator : Validator<JsonPropertyNameTransformerReviewRequest>
{
    public JsonPropertyNameTransformerReviewValidator()
    {
        RuleFor(x => x.XCoord).GreaterThan(0);
    }
}

sealed class JsonPropertyNameTransformerReviewEndpoint : Endpoint<JsonPropertyNameTransformerReviewRequest, JsonPropertyNameTransformerReviewResponse>
{
    public override void Configure()
    {
        Post("/swagger-review/json-property-name-transformers");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(JsonPropertyNameTransformerReviewRequest req, CancellationToken ct)
        => Send.OkAsync(
            new()
            {
                Secret = req.XCoord.ToString(),
                BodyValue = "ok"
            },
            ct);
}

sealed class ResponseExampleMetadataReviewResponse
{
    public string Message { get; set; } = string.Empty;
}

sealed class ResponseExampleMetadataReviewEndpoint : EndpointWithoutRequest<ResponseExampleMetadataReviewResponse>
{
    public override void Configure()
    {
        Post("/swagger-review/response-metadata-example");
        Tags("swagger_review");
        AllowAnonymous();
        Summary(s => s.Response(201, "created", example: new ResponseExampleMetadataReviewResponse { Message = "from response metadata" }));
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(new() { Message = "ok" }, ct);
}

sealed class ExplicitResponseExampleReviewEndpoint : EndpointWithoutRequest<ResponseExampleMetadataReviewResponse>
{
    public override void Configure()
    {
        Post("/swagger-review/explicit-response-example");
        Tags("swagger_review");
        AllowAnonymous();
        Summary(
            s =>
            {
                s.Response(200, example: new ResponseExampleMetadataReviewResponse { Message = "from response metadata" });
                s.ResponseExamples[200] = new ResponseExampleMetadataReviewResponse { Message = "from explicit response examples" };
            });
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(new() { Message = "ok" }, ct);
}

sealed class CollectionLengthReviewRequest
{
    public string[] Tags { get; set; } = [];
}

sealed class IntermediateBaseValidatorReviewRequest
{
    public string Name { get; set; } = string.Empty;
}

abstract class IntermediateBaseValidatorReviewValidatorBase : Validator<IntermediateBaseValidatorReviewRequest>;

sealed class IntermediateBaseValidatorReviewValidator : IntermediateBaseValidatorReviewValidatorBase
{
    public IntermediateBaseValidatorReviewValidator()
    {
        RuleFor(x => x.Name).MinimumLength(3);
    }
}

sealed class IntermediateBaseValidatorReviewEndpoint : Endpoint<IntermediateBaseValidatorReviewRequest, string>
{
    public override void Configure()
    {
        Post("/swagger-review/intermediate-base-validator");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(IntermediateBaseValidatorReviewRequest req, CancellationToken ct)
        => Send.OkAsync(req.Name, ct);
}

sealed class CollectionLengthReviewValidator : Validator<CollectionLengthReviewRequest>
{
    public CollectionLengthReviewValidator()
    {
        RuleFor(x => x.Tags).NotEmpty();
    }
}

sealed class CollectionLengthReviewEndpoint : Endpoint<CollectionLengthReviewRequest, string>
{
    public override void Configure()
    {
        Post("/swagger-review/collection-length");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(CollectionLengthReviewRequest req, CancellationToken ct)
        => Send.OkAsync(req.Tags.Length.ToString(), ct);
}

sealed class InterfaceDictionaryReviewRequest
{
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}

enum UlongEnumReviewStatus : ulong
{
    Max = ulong.MaxValue
}

sealed class UlongEnumReviewResponse
{
    public UlongEnumReviewStatus Status { get; set; }
}

sealed class UlongEnumReviewEndpoint : EndpointWithoutRequest<UlongEnumReviewResponse>
{
    public override void Configure()
    {
        Get("/swagger-review/ulong-enum");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(new() { Status = UlongEnumReviewStatus.Max }, ct);
}

sealed class InterfaceDictionaryReviewEndpoint : Endpoint<InterfaceDictionaryReviewRequest, string>
{
    public override void Configure()
    {
        Get("/swagger-review/interface-dictionary");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(InterfaceDictionaryReviewRequest req, CancellationToken ct)
        => Send.OkAsync(req.Metadata.Count.ToString(), ct);
}

/// <summary>
/// generic wrapper summary
/// </summary>
class GenericXmlDocWrapper<T>
{
    /// <summary>
    /// wrapped value summary
    /// </summary>
    /// <example>wrapped example</example>
    public T Value { get; set; } = default!;
}

sealed class GenericXmlDocReviewRequest : GenericXmlDocWrapper<string>;

/// <summary>
/// generic review response summary
/// </summary>
sealed class GenericXmlDocReviewResponse : GenericXmlDocWrapper<string>;

/// <summary>
/// returns the <c>User</c> record.
/// </summary>
sealed class InlineMarkupXmlDocReviewRequest
{
    /// <summary>
    /// filter by <paramref name="UserId" /> value.
    /// </summary>
    public string UserId { get; set; } = string.Empty;
}

sealed class InlineMarkupXmlDocReviewEndpoint : Endpoint<InlineMarkupXmlDocReviewRequest, string>
{
    public override void Configure()
    {
        Post("/swagger-review/inline-markup-xml-doc");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(InlineMarkupXmlDocReviewRequest req, CancellationToken ct)
        => Send.OkAsync(req.UserId, ct);
}

/// <summary>
/// xml endpoint summary
/// </summary>
/// <remarks>
/// xml endpoint remarks
/// </remarks>
sealed class EndpointXmlDocReviewEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/swagger-review/endpoint-xml-doc");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync("ok", ct);
}

/// <summary>
/// xml endpoint summary should not win
/// </summary>
/// <remarks>
/// xml endpoint remarks should not win
/// </remarks>
sealed class EndpointSummaryOverridesXmlDocReviewEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/swagger-review/endpoint-summary-overrides-xml-doc");
        Tags("swagger_review");
        AllowAnonymous();
        Summary(
            s =>
            {
                s.Summary = "configured endpoint summary";
                s.Description = "configured endpoint description";
            });
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync("ok", ct);
}

sealed class GenericXmlDocReviewEndpoint : Endpoint<GenericXmlDocReviewRequest, GenericXmlDocReviewResponse>
{
    public override void Configure()
    {
        Post("/swagger-review/generic-xml-doc");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(GenericXmlDocReviewRequest req, CancellationToken ct)
        => Send.OkAsync(new() { Value = req.Value }, ct);
}

sealed class MissingSchemaPrimitiveResponse
{
    public Guid CorrelationId { get; set; }
    public DateOnly EffectiveOn { get; set; }
}

sealed class MissingSchemaEnumResponse
{
    public UlongEnumReviewStatus Status { get; set; }
}

sealed class NoRequestMetadataLeakResponse
{
    /// <summary>
    /// response leak id
    /// </summary>
    public string LeakId { get; set; } = string.Empty;
}

sealed class NoRequestMetadataLeakEndpoint : EndpointWithoutRequest<NoRequestMetadataLeakResponse>
{
    public override void Configure()
    {
        Get("/swagger-review/no-request-metadata-leak/{leakId}");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(new() { LeakId = Route<string>("leakId")! }, ct);
}

sealed class DefaultRouteValueReviewRequest
{
    /// <summary>
    /// route param summary
    /// </summary>
    public string Id { get; set; } = string.Empty;
}

sealed class DefaultRouteValueReviewEndpoint : Endpoint<DefaultRouteValueReviewRequest, string>
{
    public override void Configure()
    {
        Get("/swagger-review/default-route-value/{id=5}");
        Tags("swagger_review");
        AllowAnonymous();
    }

    public override Task HandleAsync(DefaultRouteValueReviewRequest req, CancellationToken ct)
        => Send.OkAsync(req.Id, ct);
}

sealed class MissingSchemaPrimitiveEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/swagger-review/missing-schema-primitives");
        Tags("swagger_review");
        AllowAnonymous();
        Description(b => b.Produces<MissingSchemaPrimitiveResponse>(200, "application/json"));
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(ct);
}

sealed class MissingSchemaEnumEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/swagger-review/missing-schema-enum");
        Tags("swagger_review");
        AllowAnonymous();
        Description(b => b.Produces<MissingSchemaEnumResponse>(200, "application/json"));
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(ct);
}
