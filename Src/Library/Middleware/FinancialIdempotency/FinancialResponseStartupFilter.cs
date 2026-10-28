using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace FastEndpoints;

sealed class FinancialResponseStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.Use(async (ctx, nextRequest) =>
            {
                var original = ctx.Features.Get<IHttpResponseFeature>()!;
                var response = new FinancialResponseFeature(original);
                ctx.Features.Set<IHttpResponseFeature>(response);
                try { await nextRequest(ctx); }
                finally { ctx.Features.Set(original); }
            });
            next(app);
        };
}

sealed class FinancialResponseFeature : IHttpResponseFeature
{
    readonly IHttpResponseFeature _original;
    readonly Stack<(Func<object, Task> Callback, object State)> _starting = new();

    internal FinancialResponseFeature(IHttpResponseFeature original)
    {
        _original = original;
        original.OnStarting(_ => RunStartingAsync(), this);
    }

    public int StatusCode { get => _original.StatusCode; set => _original.StatusCode = value; }
    public string? ReasonPhrase { get => _original.ReasonPhrase; set => _original.ReasonPhrase = value; }
    public IHeaderDictionary Headers { get => _original.Headers; set => _original.Headers = value; }
    [Obsolete("Use IHttpResponseBodyFeature.Stream instead.")]
    public Stream Body { get => _original.Body; set => _original.Body = value; }
    public bool HasStarted => _original.HasStarted;

    public void OnStarting(Func<object, Task> callback, object state)
    {
        if (HasStarted)
            throw new InvalidOperationException("The response has already started.");
        _starting.Push((callback, state));
    }

    public void OnCompleted(Func<object, Task> callback, object state)
        => _original.OnCompleted(callback, state);

    internal void TransferStartingTo(IHttpResponseFeature response)
    {
        foreach (var callback in _starting.Reverse())
            response.OnStarting(callback.Callback, callback.State);
        _starting.Clear();
    }

    internal async Task RunStartingAsync()
    {
        try
        {
            while (_starting.TryPop(out var callback))
                await callback.Callback(callback.State);
        }
        finally
        {
            // Consumed callbacks must not run again when the transport starts.
            _starting.Clear();
        }
    }
}
