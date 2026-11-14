namespace NativeAotChecker.Endpoints.Events;

public sealed class InheritanceProgram;

public class InheritanceRequest
{
    public bool InterfaceOnly { get; set; }
    public bool Filtered { get; set; }
}

public class InheritanceResponse
{
    public int BaseCalls { get; set; }
    public int DerivedCalls { get; set; }
    public int InterfaceCalls { get; set; }
}

public interface IInheritanceEvent : IEvent
{
    InheritanceResponse Result { get; }
}

public class InheritanceBase : IEvent
{
    public InheritanceResponse Result { get; } = new();
}

public sealed class InheritanceDerived : InheritanceBase, IInheritanceEvent;
public sealed class InterfaceOnlyEvent : IInheritanceEvent
{
    public InheritanceResponse Result { get; } = new();
}

public sealed class InheritanceDualHandler : IEventHandler<InheritanceBase>, IEventHandler<InheritanceDerived>
{
    Task IEventHandler<InheritanceBase>.HandleAsync(InheritanceBase e, CancellationToken ct)
    {
        e.Result.BaseCalls++;
        return Task.CompletedTask;
    }

    Task IEventHandler<InheritanceDerived>.HandleAsync(InheritanceDerived e, CancellationToken ct)
    {
        e.Result.DerivedCalls++;
        return Task.CompletedTask;
    }
}

public sealed class InheritanceInterfaceHandler : IEventHandler<IInheritanceEvent>
{
    public Task HandleAsync(IInheritanceEvent e, CancellationToken ct)
    {
        e.Result.InterfaceCalls++;
        return Task.CompletedTask;
    }
}

public sealed class EventInheritanceEndpoint : Endpoint<InheritanceRequest, InheritanceResponse>
{
    public override void Configure()
    {
        Post("event-inheritance");
        AllowAnonymous();
    }

    public override async Task HandleAsync(InheritanceRequest req, CancellationToken ct)
    {
        if (req.InterfaceOnly)
        {
            var model = new InterfaceOnlyEvent();
            await PublishAsync<IEvent>(model, Mode.WaitForAll, ct);
            await Send.OkAsync(model.Result, ct);
        }
        else
        {
            InheritanceBase model = new InheritanceDerived();
            if (req.Filtered)
                await model.PublishFilteredAsync(t => t == typeof(InheritanceDualHandler), Mode.WaitForAll, ct);
            else
                await PublishAsync(model, Mode.WaitForAll, ct);
            await Send.OkAsync(model.Result, ct);
        }
    }
}
