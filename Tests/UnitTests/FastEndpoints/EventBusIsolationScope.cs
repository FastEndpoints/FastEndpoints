using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace EventBus;

sealed class EventBusIsolationScope : IDisposable
{
    readonly IServiceResolver? _previousResolver = ServiceResolver.InstanceNotSet ? null : ServiceResolver.Instance;
    readonly HttpContext? _previousContext = new HttpContextAccessor().HttpContext;

    public EventBusIsolationScope() => new HttpContextAccessor().HttpContext = null;

    public void Dispose()
    {
        ServiceResolver.Instance = _previousResolver!;
        new HttpContextAccessor().HttpContext = _previousContext;
    }
}
