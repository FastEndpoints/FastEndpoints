namespace TestCases.CommandHandlerTest;

// with result

sealed class GenericCommand<TResult> : ICommand<IEnumerable<TResult>> where TResult : new() { }

sealed class GenericCommandHandler<TResult> : ICommandHandler<GenericCommand<TResult>, IEnumerable<TResult>> where TResult : new()
{
    public Task<IEnumerable<TResult>> ExecuteAsync(GenericCommand<TResult> command, CancellationToken ct)
    {
        var res = new List<TResult> { new(), new(), new() };

        return Task.FromResult(res.AsEnumerable());
    }
}

sealed class GenericCmdEndpoint : EndpointWithoutRequest<IEnumerable<Guid>>
{
    public override void Configure()
    {
        Get("/tests/generic-command-handler");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken c)
    {
        var cmd = new GenericCommand<Guid>();
        var list = await cmd.ExecuteAsync();
        await SendAsync(list);
    }
}

// without result

sealed class GenericNoResultCommand<T> : ICommand where T : new()
{
    public T Id { get; set; } = default!;
}

sealed class GenericNoResultCommandHandler<T> : ICommandHandler<GenericNoResultCommand<T>> where T : new()
{
    public Task ExecuteAsync(GenericNoResultCommand<T> command, CancellationToken ct)
    {
        command.Id = new();

        return Task.CompletedTask;
    }
}

sealed class GenericCmdWithoutResultEndpoint : EndpointWithoutRequest<Guid>
{
    public override void Configure()
    {
        Get("/tests/generic-command-handler-without-result");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken c)
    {
        var cmd = new GenericNoResultCommand<Guid>
        {
            Id = Guid.NewGuid()
        };

        await cmd.ExecuteAsync();
        await SendAsync(cmd.Id);
    }
}