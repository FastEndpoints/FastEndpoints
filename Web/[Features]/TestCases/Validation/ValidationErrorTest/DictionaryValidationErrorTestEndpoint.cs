namespace TestCases.ValidationErrorTest;

public class DictionaryRequest
{
    public Dictionary<string, string> StringDictionary { get; init; }
}

public class DictionaryValidationErrorTestEndpoint : Endpoint<DictionaryRequest>
{
    public override void Configure()
    {
        Post("/test-cases/dictionary-validation-error-test");
    }

    public override Task HandleAsync(DictionaryRequest req, CancellationToken c)
    {

        foreach (var (key, val) in req.StringDictionary)
        {
            AddError(r => r.StringDictionary[key], "Invalid");
        }
        
        return Task.CompletedTask;
    }
}