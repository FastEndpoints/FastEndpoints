namespace NativeAotChecker.Endpoints.Processors;

public sealed class PreProcessorRequest
{
    public string InputValue { get; set; } = string.Empty;
}

public sealed class PreProcessorResponse
{
    public string ResultValue { get; set; } = string.Empty;
    public bool PreProcessorExecuted { get; set; }
}

public sealed class PreProcessorEndpoint : Endpoint<PreProcessorRequest, PreProcessorResponse>
{
    public override void Configure()
    {
        Post("pre-post-processor");
        AllowAnonymous();
        PreProcessor<PreProcessor>();
    }

    public override async Task HandleAsync(PreProcessorRequest r, CancellationToken c)
    {
        var preProcessorExecuted = HttpContext.Items.TryGetValue("PreProcessorExecuted", out var preValue) && preValue is true;

        await Send.OkAsync(
            new()
            {
                ResultValue = $"PROCESSED:{r.InputValue}",
                PreProcessorExecuted = preProcessorExecuted
            },
            c);
    }
}

public sealed class PreProcessor : IPreProcessor<PreProcessorRequest>
{
    public Task PreProcessAsync(IPreProcessorContext<PreProcessorRequest> context, CancellationToken cancellationToken)
    {
        context.HttpContext.Items["PreProcessorExecuted"] = true;

        return Task.CompletedTask;
    }
}