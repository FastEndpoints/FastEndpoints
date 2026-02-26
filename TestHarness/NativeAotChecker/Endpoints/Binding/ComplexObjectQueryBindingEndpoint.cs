namespace NativeAotChecker.Endpoints.Binding;

public class ComplexObjectQueryBindingRequest
{
    [QueryParam]
    public Person Person { get; set; } = null!;

    [QueryParam]
    public string? Category { get; set; }
}

public class Person
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public int Age { get; set; }
}

public class ComplexObjectQueryBindingResponse
{
    public Person Person { get; set; } = null!;
    public string? Category { get; set; }
}

public class ComplexObjectQueryBindingEndpoint : Endpoint<ComplexObjectQueryBindingRequest, ComplexObjectQueryBindingResponse>
{
    public override void Configure()
    {
        Get("complex-object-query-binding");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ComplexObjectQueryBindingRequest req, CancellationToken ct)
    {
        await Send.OkAsync(
            new()
            {
                Person = req.Person,
                Category = req.Category
            },
            ct);
    }
}