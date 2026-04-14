using FastEndpoints.Swagger;

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