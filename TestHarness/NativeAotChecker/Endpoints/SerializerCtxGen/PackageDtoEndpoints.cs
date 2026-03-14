using Scalar.AspNetCore;

namespace NativeAotChecker.Endpoints.SerializerCtxGen;

sealed class PackageDtoEnvelope
{
    public ScalarDocument? Document { get; set; }
}

sealed class PackageDtoEnvelopeResponse
{
    public string? Summary { get; set; }
    public ScalarDocument? Document { get; set; }
}

sealed class PackageDtoDirectEndpoint : Endpoint<ScalarDocument, ScalarDocument>
{
    public override void Configure()
    {
        Post("ser-ctx-gen-package-direct");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ScalarDocument req, CancellationToken ct)
    {
        await Send.OkAsync(
            new($"copy:{req.Name}", $"echo:{req.Title}", req.RoutePattern, req.IsDefault),
            ct);
    }
}

sealed class PackageDtoNestedEndpoint : Endpoint<PackageDtoEnvelope, PackageDtoEnvelopeResponse>
{
    private static readonly ScalarDocument _defaultDocument = new("default-doc", "default-title", "/scalar/default", false);

    public override void Configure()
    {
        Post("ser-ctx-gen-package-nested");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PackageDtoEnvelope req, CancellationToken ct)
    {
        var document = req.Document ?? _defaultDocument;

        await Send.OkAsync(
            new()
            {
                Summary = $"{document.Name}|{document.IsDefault}",
                Document = new($"nested:{document.Name}", document.Title, document.RoutePattern, document.IsDefault)
            },
            ct);
    }
}
