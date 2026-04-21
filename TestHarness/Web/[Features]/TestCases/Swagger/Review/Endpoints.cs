using FluentValidation;
using FastEndpoints.OpenApi;
using System.Collections.Generic;

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
        => RuleFor(x => x.Score).GreaterThan(10);
}

sealed class ChildValidatorReviewValidator : Validator<ChildValidatorReviewRequest>
{
    public ChildValidatorReviewValidator()
        => RuleFor(x => x.Child).SetValidator(new ChildValidatorReviewChildValidator());
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

sealed class InterfaceDictionaryReviewRequest
{
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
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
