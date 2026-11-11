using FluentValidation.Results;

namespace NativeAotChecker.Endpoints.SerializerCtxGen;

sealed class PackageDtoEnvelope
{
    public ValidationFailure? Failure { get; set; }
}

sealed class PackageDtoEnvelopeResponse
{
    public string? Summary { get; set; }
    public ValidationFailure? Failure { get; set; }
}

sealed class PackageDtoDirectEndpoint : Endpoint<ValidationFailure, ValidationFailure>
{
    public override void Configure()
    {
        Post("ser-ctx-gen-package-direct");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ValidationFailure req, CancellationToken ct)
    {
        await Send.OkAsync(
            new($"copy:{req.PropertyName}", $"echo:{req.ErrorMessage}")
            {
                ErrorCode = req.ErrorCode,
                Severity = req.Severity
            },
            ct);
    }
}

sealed class PackageDtoNestedEndpoint : Endpoint<PackageDtoEnvelope, PackageDtoEnvelopeResponse>
{
    private static readonly ValidationFailure _defaultFailure = new("default-property", "default-message");

    public override void Configure()
    {
        Post("ser-ctx-gen-package-nested");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PackageDtoEnvelope req, CancellationToken ct)
    {
        var failure = req.Failure ?? _defaultFailure;

        await Send.OkAsync(
            new()
            {
                Summary = $"{failure.PropertyName}|{failure.Severity}",
                Failure = new($"nested:{failure.PropertyName}", failure.ErrorMessage)
                {
                    ErrorCode = failure.ErrorCode,
                    Severity = failure.Severity
                }
            },
            ct);
    }
}
