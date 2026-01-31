using Void = FastEndpoints.Void;

namespace TestCases.CommandHandlerTest;

[RegisterService<MyScopedService>(LifeTime.Scoped)]
public class MyScopedService
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

sealed class ScopedServiceCheckEndpoint(MyScopedService service) : EndpointWithoutRequest<Guid>
{
    public override void Configure()
    {
        Get("tests/scoped-service-check");
        AllowAnonymous();
    }

    public override async Task<Void> HandleAsync(CancellationToken ct)
    {
        var svcId = service.InstanceId;
        var res = await new ScopedServiceCheckCommand().ExecuteAsync(ct);

        if (svcId != res)
            return await Send.ErrorsAsync(400, ct);

        return await Send.OkAsync(res, ct);
    }
}

sealed class ScopedServiceCheckCommand : ICommand<Guid>;

sealed class ScopedServiceCheckCommandHandler(MyScopedService service) : ICommandHandler<ScopedServiceCheckCommand, Guid>
{
    public Task<Guid> ExecuteAsync(ScopedServiceCheckCommand cmd, CancellationToken c)
        => Task.FromResult(service.InstanceId);
}