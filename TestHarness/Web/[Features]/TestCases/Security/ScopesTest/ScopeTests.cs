namespace TestCases.ScopesTest;

public class ScopeTestAnyPassEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/test-cases/scope-tests/any-scope-pass");
        Scopes("two", "three", "blah");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync("ok!");
    }
}

public class ScopeTestAnyFailEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/test-cases/scope-tests/any-scope-fail");
        Scopes("nine");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync("ok!");
    }
}

public class ScopeTestAllPassEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/test-cases/scope-tests/all-scope-pass");
        ScopesAll("one", "two", "three");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync("ok!");
    }
}

public class ScopeTestAllFailEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/test-cases/scope-tests/all-scope-fail");
        ScopesAll("one", "two", "three", "blah");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync("ok!");
    }
}