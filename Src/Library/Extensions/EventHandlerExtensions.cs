namespace FastEndpoints;

public delegate Task AsyncEventHandler<TEventArgs>(TEventArgs args, CancellationToken cancellation);

internal static class EventHandlerExtensions
{
    internal static IEnumerable<AsyncEventHandler<TEventArgs>> GetHandlers<TEventArgs>(this AsyncEventHandler<TEventArgs> handler)
        => handler.GetInvocationList().Cast<AsyncEventHandler<TEventArgs>>();

    internal static Task InvokeAllAsync<TEventArgs>(this AsyncEventHandler<TEventArgs> handler, TEventArgs args, CancellationToken cancellation)
    {
        var tasks = new List<Task>();
        foreach (var h in handler.GetHandlers())
        {
            tasks.Add(h(args, cancellation));
        }
        return Task.WhenAll(tasks);
    }

    internal static Task InvokeAnyAsync<TEventArgs>(this AsyncEventHandler<TEventArgs> handler, TEventArgs args, CancellationToken cancellation)
    {
        var tasks = new List<Task>();
        foreach (var h in handler.GetHandlers())
        {
            tasks.Add(h(args, cancellation));
        }
        return Task.WhenAny(tasks);
    }
}