namespace TestCases.JobQueueTest;

public class JobWithResultTestCommand : ICommand<Guid>
{
    public Guid Id { get; set; }
}

public class JobWithResultTestCommandHandler : ICommandHandler<JobWithResultTestCommand, Guid>
{
    public Task<Guid> ExecuteAsync(JobWithResultTestCommand cmd, CancellationToken ct)
        => Task.FromResult(cmd.Id);
}