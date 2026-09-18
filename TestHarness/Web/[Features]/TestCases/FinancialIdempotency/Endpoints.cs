using FluentValidation;

namespace TestCases.FinancialIdempotency;

sealed class ChargeRequest
{
    public int Amount { get; set; }
    public string? Note { get; set; }
}

sealed class ChargeResponse
{
    public int Amount { get; set; }
    public long Ticks { get; set; }
}

sealed class ChargeEndpoint : Endpoint<ChargeRequest, ChargeResponse>
{
    public static int HandleCount;

    public override void Configure()
    {
        Post("test-cases/financial-idempotency/charge");
        AllowAnonymous();
        Description(x => x.ExcludeFromDescription());
        FinancialIdempotency(
            o =>
            {
                o.ReplayStatusCode = 200;
            });
    }

    public override Task HandleAsync(ChargeRequest req, CancellationToken ct)
    {
        Interlocked.Increment(ref HandleCount);

        return Send.ResponseAsync(
            new()
            {
                Amount = req.Amount,
                Ticks = DateTime.UtcNow.Ticks
            },
            201);
    }
}

sealed class FormEndpoint : Endpoint<ChargeRequest, ChargeResponse>
{
    public static int HandleCount;

    public override void Configure()
    {
        Post("test-cases/financial-idempotency/form");
        AllowAnonymous();
        AllowFormData(urlEncoded: true);
        Description(x => x.ExcludeFromDescription());
        FinancialIdempotency();
    }

    public override Task HandleAsync(ChargeRequest req, CancellationToken ct)
    {
        Interlocked.Increment(ref HandleCount);

        return Send.ResponseAsync(
            new()
            {
                Amount = req.Amount,
                Ticks = DateTime.UtcNow.Ticks
            },
            201);
    }
}

sealed class OversizeEndpoint : EndpointWithoutRequest
{
    public static int HandleCount;

    public override void Configure()
    {
        Post("test-cases/financial-idempotency/oversize");
        AllowAnonymous();
        Description(x => x.ExcludeFromDescription());
        FinancialIdempotency(o => o.MaxResponseBodySize = 16);
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref HandleCount);

        return Send.OkAsync(new string('x', 64));
    }
}

sealed class ThrowEndpoint : Endpoint<ChargeRequest, ChargeResponse>
{
    public static int HandleCount;

    public override void Configure()
    {
        Post("test-cases/financial-idempotency/throw");
        AllowAnonymous();
        Description(x => x.ExcludeFromDescription());
        FinancialIdempotency();
    }

    public override Task HandleAsync(ChargeRequest req, CancellationToken ct)
    {
        Interlocked.Increment(ref HandleCount);

        if (req.Amount < 0)
            throw new InvalidOperationException("simulated handler failure");

        return Send.ResponseAsync(
            new()
            {
                Amount = req.Amount,
                Ticks = DateTime.UtcNow.Ticks
            },
            201);
    }
}

sealed class InvalidRequest
{
    public int Amount { get; set; }
}

sealed class InvalidChargeValidator : Validator<InvalidRequest>
{
    public InvalidChargeValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

sealed class InvalidEndpoint : Endpoint<InvalidRequest, ChargeResponse>
{
    public static int HandleCount;

    public override void Configure()
    {
        Post("test-cases/financial-idempotency/invalid");
        AllowAnonymous();
        Validator<InvalidChargeValidator>();
        Description(x => x.ExcludeFromDescription());
        FinancialIdempotency();
    }

    public override Task HandleAsync(InvalidRequest req, CancellationToken ct)
    {
        Interlocked.Increment(ref HandleCount);

        return Send.ResponseAsync(
            new()
            {
                Amount = req.Amount,
                Ticks = DateTime.UtcNow.Ticks
            },
            201);
    }
}

sealed class ChargePreProc : IPreProcessor<ChargeRequest>
{
    public Task PreProcessAsync(IPreProcessorContext<ChargeRequest> ctx, CancellationToken ct)
    {
        Interlocked.Increment(ref ProcessorEndpoint.PreCount);

        return Task.CompletedTask;
    }
}

sealed class ChargePostProc : IPostProcessor<ChargeRequest, ChargeResponse>
{
    public Task PostProcessAsync(IPostProcessorContext<ChargeRequest, ChargeResponse> ctx, CancellationToken ct)
    {
        Interlocked.Increment(ref ProcessorEndpoint.PostCount);

        return Task.CompletedTask;
    }
}

sealed class ProcessorEndpoint : Endpoint<ChargeRequest, ChargeResponse>
{
    public static int HandleCount;
    public static int PreCount;
    public static int PostCount;

    public override void Configure()
    {
        Post("test-cases/financial-idempotency/processors");
        AllowAnonymous();
        PreProcessor<ChargePreProc>();
        PostProcessor<ChargePostProc>();
        Description(x => x.ExcludeFromDescription());
        FinancialIdempotency();
    }

    public override Task HandleAsync(ChargeRequest req, CancellationToken ct)
    {
        Interlocked.Increment(ref HandleCount);

        return Send.ResponseAsync(
            new()
            {
                Amount = req.Amount,
                Ticks = DateTime.UtcNow.Ticks
            },
            201);
    }
}

sealed class CaseSensitiveEndpoint : Endpoint<ChargeRequest, ChargeResponse>
{
    public static int HandleCount;

    public override void Configure()
    {
        Post("test-cases/financial-idempotency/cased");
        AllowAnonymous();
        Description(x => x.ExcludeFromDescription());
        FinancialIdempotency(o => o.UseCaseSensitivePaths = true);
    }

    public override Task HandleAsync(ChargeRequest req, CancellationToken ct)
    {
        Interlocked.Increment(ref HandleCount);

        return Send.ResponseAsync(
            new()
            {
                Amount = req.Amount,
                Ticks = DateTime.UtcNow.Ticks
            },
            201);
    }
}

sealed class StreamEndpoint : EndpointWithoutRequest
{
    public static int HandleCount;

    public override void Configure()
    {
        Post("test-cases/financial-idempotency/stream");
        AllowAnonymous();
        DontAutoSendResponse();
        Description(x => x.ExcludeFromDescription());
        FinancialIdempotency();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref HandleCount);
        HttpContext.MarkResponseStart();
        HttpContext.Response.StatusCode = 201;
        HttpContext.Response.ContentType = "text/plain";
        await HttpContext.Response.WriteAsync("streamed", ct);
    }
}

sealed class ConcurrentEndpoint : Endpoint<ChargeRequest, ChargeResponse>
{
    public static int HandleCount;
    public static TaskCompletionSource? Entered;
    public static TaskCompletionSource? Release;

    public override void Configure()
    {
        Post("test-cases/financial-idempotency/concurrent");
        AllowAnonymous();
        Description(x => x.ExcludeFromDescription());
        FinancialIdempotency();
    }

    public override async Task HandleAsync(ChargeRequest req, CancellationToken ct)
    {
        Interlocked.Increment(ref HandleCount);
        Entered?.TrySetResult();

        if (Release is not null)
            await Release.Task.WaitAsync(ct);

        await Send.ResponseAsync(
            new()
            {
                Amount = req.Amount,
                Ticks = DateTime.UtcNow.Ticks
            },
            201);
    }
}

sealed class RawEndpoint : EndpointWithoutRequest<ChargeResponse>
{
    public static int HandleCount;

    public override void Configure()
    {
        Post("test-cases/financial-idempotency/raw");
        AllowAnonymous();
        Description(x => x.ExcludeFromDescription());
        FinancialIdempotency();
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref HandleCount);

        return Send.ResponseAsync(
            new()
            {
                Amount = 0,
                Ticks = DateTime.UtcNow.Ticks
            },
            201);
    }
}
