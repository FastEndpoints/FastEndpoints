namespace NativeAotChecker.Endpoints;

// Test tuple response serialization - likely AOT issue
public sealed class TupleRequest
{
    public int A { get; set; }
    public int B { get; set; }
}

public sealed class TupleResponseEndpoint : EndpointWithoutRequest<(int Sum, int Product, int Difference)>
{
    public override void Configure()
    {
        Get("tuple-response");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var a = Query<int>("a", isRequired: false);
        var b = Query<int>("b", isRequired: false);
        
        await Send.OkAsync((Sum: a + b, Product: a * b, Difference: a - b));
    }
}
