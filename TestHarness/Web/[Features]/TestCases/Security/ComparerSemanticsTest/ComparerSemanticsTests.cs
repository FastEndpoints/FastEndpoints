namespace TestCases.ComparerSemanticsTest;

//permission values must match with ordinal (case-sensitive) comparison

public class PermissionCasingExactEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/test-cases/comparer-tests/permission-casing-exact");
        Permissions("CasePerm");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync("ok!");
    }
}

public class PermissionCasingMismatchEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/test-cases/comparer-tests/permission-casing-mismatch");
        Permissions("caseperm");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync("ok!");
    }
}

//scope values must match with case-insensitive comparison

public class ScopeCasingMismatchEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/test-cases/comparer-tests/scope-casing-mismatch");
        Scopes("TWO");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync("ok!");
    }
}

//claim types must match with case-insensitive comparison

public class ClaimTypeCasingMismatchEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/test-cases/comparer-tests/claim-type-casing-mismatch");
        Claims("CUSTOMER-ID");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync("ok!");
    }
}
