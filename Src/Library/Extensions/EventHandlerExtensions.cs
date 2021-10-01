namespace FastEndpoints
{
    public delegate Task AsyncEventHandler<TEventArgs>(TEventArgs args, CancellationToken cancellation);

    internal static class EventHandlerExtensions
    {
        internal static IEnumerable<AsyncEventHandler<TEventArgs>> GetHandlers<TEventArgs>(this AsyncEventHandler<TEventArgs> handler)
            => handler.GetInvocationList().Cast<AsyncEventHandler<TEventArgs>>();

        internal static Task InvokeAllAsync<TEventArgs>(this AsyncEventHandler<TEventArgs> handler, TEventArgs args, CancellationToken cancellation)
            => Task.WhenAll(handler.GetHandlers().Select(h => h(args, cancellation)));

        internal static Task InvokeAnyAsync<TEventArgs>(this AsyncEventHandler<TEventArgs> handler, TEventArgs args, CancellationToken cancellation)
            => Task.WhenAny(handler.GetHandlers().Select(h => h(args, cancellation)));
    }
}
