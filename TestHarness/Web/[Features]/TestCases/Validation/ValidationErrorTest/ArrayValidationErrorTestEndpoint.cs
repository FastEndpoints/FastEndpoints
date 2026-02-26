namespace TestCases.ValidationErrorTest;

public class ArrayRequest
{
    public string[] StringArray { get; init; }
}

public class ArrayValidationErrorTestEndpoint : Endpoint<ArrayRequest>
{
    public override void Configure()
    {
        Post("/test-cases/array-validation-error-test");
    }

    public override Task HandleAsync(ArrayRequest req, CancellationToken c)
    {
        for (int i = 0; i < req.StringArray.Length; i++)
        {
            AddError(r => r.StringArray[i], "Invalid");
        }
        return Task.CompletedTask;
    }
}