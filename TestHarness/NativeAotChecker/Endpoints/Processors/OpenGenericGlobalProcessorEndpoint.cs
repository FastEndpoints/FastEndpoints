namespace NativeAotChecker.Endpoints.Processors;

public sealed class OpenGenericGlobalProcessorRequest
{
    public string InputValue { get; set; } = string.Empty;
}

public sealed class OpenGenericGlobalProcessorResponse
{
    public string ResultValue { get; set; } = string.Empty;
    public bool GlobalPreProcessorExecuted { get; set; }
}

public sealed class OpenGenericGlobalProcessorEndpoint : Endpoint<OpenGenericGlobalProcessorRequest, OpenGenericGlobalProcessorResponse>
{
    public override void Configure()
    {
        Post("open-generic-global-processor");
        AllowAnonymous();
    }

    public override async Task HandleAsync(OpenGenericGlobalProcessorRequest r, CancellationToken c)
    {
        var globalPreProcessorExecuted = HttpContext.Items.TryGetValue("OpenGenericGlobalPreProcessorExecuted", out var preValue) && preValue is true;

        await Send.OkAsync(
            new()
            {
                ResultValue = $"PROCESSED:{r.InputValue}",
                GlobalPreProcessorExecuted = globalPreProcessorExecuted
            },
            c);
    }
}