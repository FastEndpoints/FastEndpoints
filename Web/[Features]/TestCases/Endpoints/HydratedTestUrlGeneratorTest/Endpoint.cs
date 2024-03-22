namespace TestCases.HydratedTestUrlGeneratorTest;

public class Endpoint : Endpoint<Request, string>
{
    public override void Configure()
    {
        Verbs(Http.GET, Http.POST, Http.PUT, Http.PATCH, Http.DELETE);
        Routes("/test/hydrated-test-url-generator-test/{id}/{guid:guid}/{stringBindFrom}/{nullableString}/{fromClaim}/{fromHeader}/{hasPermission}");
        AllowAnonymous();
    }

    public override Task<string> ExecuteAsync(Request req, CancellationToken ct)
        => Task.FromResult(HttpContext.Request.Path.Value!);
}

public class Request
{
    public int Id { get; set; }
    public Guid Guid { get; set; }

    [BindFrom("stringBindFrom")]
    public string String { get; set; } = null!;

    public string? NullableString { get; set; }

    [FromClaim(Claim.UserType)]
    public string FromClaim { get; set; }

    [FromHeader("tenant-id")]
    public string FromHeader { get; set; }

    [HasPermission(Allow.Customers_Create, IsRequired = false)]
    public bool? HasPermission { get; set; }
}