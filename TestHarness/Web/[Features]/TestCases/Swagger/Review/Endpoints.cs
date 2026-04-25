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
            b => b.WithOpenApi(
                operation =>
                {
                    operation.Parameters ??= [];
                    operation.Parameters.Add(
                        new OpenApiParameter
                        {
                            Name = "firstName",
                            In = ParameterLocation.Query,
                            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                        });

                    return operation;
                }));
    }

    public override Task HandleAsync(DuplicateQueryNamingPolicyRequest req, CancellationToken ct)
        => Send.OkAsync(req.FirstName, ct);
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