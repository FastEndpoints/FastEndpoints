namespace TestCases.ValidationErrorTest;

public class ListRequest
{
    public List<int> NumbersList { get; init; }
}

public class ListValidationErrorTestEndpoint : Endpoint<ListRequest>
{
    public override void Configure()
    {
        Post("/test-cases/list-validation-error-test");
    }

    public override Task HandleAsync(ListRequest req, CancellationToken c)
    {

        for (int i = 0; i < req.NumbersList.Count; i++)
        {
            AddError(r => r.NumbersList[i], "Invalid");
        }
        return Task.CompletedTask;
    }
}