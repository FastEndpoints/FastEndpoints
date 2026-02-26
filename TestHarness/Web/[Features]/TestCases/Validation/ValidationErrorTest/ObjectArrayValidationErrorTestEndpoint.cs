namespace TestCases.ValidationErrorTest;

public class ObjectArrayRequest
{
    public TObject[] ObjectArray { get; init; }
}

public class TObject
{
    public string Test { get; init; }
}

public class ObjectArrayValidationErrorTestEndpoint : Endpoint<ObjectArrayRequest>
{
    public override void Configure()
    {
        Post("/test-cases/object-array-validation-error-test");
    }

    public override Task HandleAsync(ObjectArrayRequest req, CancellationToken c)
    {
        for (int i = 0; i < req.ObjectArray.Length; i++)
        {
            AddError(r => r.ObjectArray[i].Test, "Invalid");
        }
        return Task.CompletedTask;
    }
}