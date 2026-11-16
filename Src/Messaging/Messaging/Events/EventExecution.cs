namespace FastEndpoints;

interface IEventInvocation<THandler, TModel>
{
    static abstract Task Invoke(THandler handler, TModel model, CancellationToken ct);
}

static class EventExecution<THandler, TModel, TInvocation> where TInvocation : struct, IEventInvocation<THandler, TModel>
{
    internal static Task Execute(THandler[] handlers, TModel model, Mode mode, CancellationToken ct)
    {
        if (handlers.Length == 0)
            return Task.CompletedTask;

        switch (mode)
        {
            case Mode.WaitForNone:
                Offload(handlers, model, ct);
                return Task.CompletedTask;
            case Mode.WaitForAny:
                // Keep WhenAny even for one handler so its faults are not surfaced.
                return handlers.Length == 1
                           ? Task.WhenAny(OffloadOne(handlers[0], model, ct))
                           : Task.WhenAny(OffloadAll(handlers, model, ct));
            case Mode.WaitForAll:
                if (handlers.Length == 1)
                    return TInvocation.Invoke(handlers[0], model, ct);

                var tasks = new Task[handlers.Length];
                for (var i = 0; i < handlers.Length; i++)
                    tasks[i] = TInvocation.Invoke(handlers[i], model, ct);
                return Task.WhenAll(tasks);
            default:
                return Task.CompletedTask;
        }
    }

    // Separate closures from Execute to keep the synchronous single-handler path allocation-free.
    static Task OffloadOne(THandler handler, TModel model, CancellationToken ct)
        => Task.Run(() => TInvocation.Invoke(handler, model, ct), ct);

    static void Offload(THandler[] handlers, TModel model, CancellationToken ct)
    {
        foreach (var handler in handlers)
            _ = OffloadOne(handler, model, ct);
    }

    static Task[] OffloadAll(THandler[] handlers, TModel model, CancellationToken ct)
    {
        var tasks = new Task[handlers.Length];
        for (var i = 0; i < handlers.Length; i++)
            tasks[i] = OffloadOne(handlers[i], model, ct);
        return tasks;
    }
}
