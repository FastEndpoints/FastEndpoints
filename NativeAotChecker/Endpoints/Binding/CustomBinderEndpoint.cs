namespace NativeAotChecker.Endpoints.Binding;

public sealed class CustomBinderRequest
{
    public string InputValue { get; set; }
    public string ProcessedValue { get; set; }
}

public sealed class CustomBinderResponse
{
    public string InputValue { get; set; }
    public string? ProcessedValue { get; set; }
    public bool BinderWasUsed { get; set; }
}

public sealed class CustomBinder : RequestBinder<CustomBinderRequest>
{
    public override async ValueTask<CustomBinderRequest> BindAsync(BinderContext ctx, CancellationToken ct)
    {
        await ValueTask.CompletedTask;

        var headerValue = ctx.HttpContext.Request.Headers["X-Custom-Value"].ToString();

        return new()
        {
            InputValue = headerValue,
            ProcessedValue = $"CUSTOM-BINDER:{headerValue}"
        };
    }
}

public sealed class CustomBinderEndpoint : Endpoint<CustomBinderRequest, CustomBinderResponse>
{
    public override void Configure()
    {
        Post("custom-binder-test");
        AllowAnonymous();
        RequestBinder(new CustomBinder());
    }

    public override async Task HandleAsync(CustomBinderRequest r, CancellationToken ct)
    {
        var binderWasUsed = r.ProcessedValue.StartsWith("CUSTOM-BINDER:");

        await Send.OkAsync(
            new()
            {
                InputValue = r.InputValue,
                ProcessedValue = r.ProcessedValue,
                BinderWasUsed = binderWasUsed
            },
            ct);
    }
}